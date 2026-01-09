using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Plugin.CSharpScript;

/// <summary>
/// Manages the lifecycle of dynamically loaded .cs plugins with hot reload support.
/// Watches the Plugins directory for .cs file changes and automatically reloads.
/// </summary>
public class CsPluginServiceHost : ICsPluginServiceHost
{
    private readonly string _pluginsDirectory;
    private readonly FileSystemWatcher _watcher;
    private readonly Dictionary<string, CsPluginInstance> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();
    private readonly CsPluginCompiler _compiler;
    private readonly IServiceProvider _rootServiceProvider;
    private readonly ILogger<CsPluginServiceHost> _logger;

    // Debounce file changes (editors often save multiple times, copy triggers both Created and Changed)
    private readonly Dictionary<string, DateTime> _lastChangeTime = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(500);

    // Per-file semaphores to prevent concurrent operations on the same file
    private readonly Dictionary<string, SemaphoreSlim> _fileOperationLocks = new(StringComparer.OrdinalIgnoreCase);

    public CsPluginServiceHost(
        IServiceProvider serviceProvider,
        CsPluginCompiler compiler,
        ILogger<CsPluginServiceHost> logger)
    {
        _rootServiceProvider = serviceProvider;
        _compiler = compiler;
        _logger = logger;
        _pluginsDirectory = Path.Combine(Utilities.OperatingDirectory, "Plugins");

        if (!Directory.Exists(_pluginsDirectory))
        {
            Directory.CreateDirectory(_pluginsDirectory);
        }

        // Set up file watcher for the plugins directory
        _watcher = new FileSystemWatcher(_pluginsDirectory, "*.cs")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = false,
            IncludeSubdirectories = false
        };

        _watcher.Changed += OnPluginFileChanged;
        _watcher.Created += OnPluginFileCreated;
        _watcher.Deleted += OnPluginFileDeleted;
        _watcher.Renamed += OnPluginFileRenamed;
    }

    /// <inheritdoc />
    public IEnumerable<IPluginV2> LoadedPlugins
    {
        get
        {
            lock (_lock)
            {
                return _plugins.Values
                    .Where(p => p.Plugin != null)
                    .Select(p => p.Plugin!)
                    .ToList();
            }
        }
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting C# script plugin service host");
        _logger.LogInformation("Plugins directory: {Directory}", _pluginsDirectory);

        // Load all existing .cs plugins
        var pluginFiles = Directory.GetFiles(_pluginsDirectory, "*.cs");

        if (pluginFiles.Length == 0)
        {
            _logger.LogInformation("No .cs plugins found in Plugins directory");
        }
        else
        {
            _logger.LogInformation("Found {Count} .cs plugin(s)", pluginFiles.Length);

            foreach (var pluginPath in pluginFiles)
            {
                try
                {
                    await LoadPluginAsync(pluginPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load .cs plugin {FileName}", Path.GetFileName(pluginPath));
                }
            }
        }

        // Enable file watching for hot reload
        _watcher.EnableRaisingEvents = true;
        _logger.LogInformation("Hot reload enabled - watching for .cs file changes");
    }

    private void OnPluginFileChanged(object sender, FileSystemEventArgs e)
    {
        HandleFileEventAsync(e.FullPath, FileEventType.Changed);
    }

    private void OnPluginFileCreated(object sender, FileSystemEventArgs e)
    {
        HandleFileEventAsync(e.FullPath, FileEventType.Created);
    }

    private void OnPluginFileDeleted(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation("Plugin removed: {FileName}", e.Name);

        _ = Task.Run(async () =>
        {
            var semaphore = GetOrCreateFileLock(e.FullPath);
            await semaphore.WaitAsync();
            try
            {
                await UnloadPluginAsync(e.FullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unloading plugin {FileName}", e.Name);
            }
            finally
            {
                semaphore.Release();
            }
        });
    }

    private void OnPluginFileRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogInformation("Plugin renamed: {OldName} → {NewName}", e.OldName, e.Name);

        _ = Task.Run(async () =>
        {
            // Lock both old and new paths
            var oldSemaphore = GetOrCreateFileLock(e.OldFullPath);
            var newSemaphore = GetOrCreateFileLock(e.FullPath);

            await oldSemaphore.WaitAsync();
            try
            {
                await UnloadPluginAsync(e.OldFullPath);
            }
            finally
            {
                oldSemaphore.Release();
            }

            // Load with new name if it's still a .cs file
            if (e.FullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                await newSemaphore.WaitAsync();
                try
                {
                    await Task.Delay(100);
                    await LoadPluginAsync(e.FullPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading renamed plugin {FileName}", e.Name);
                }
                finally
                {
                    newSemaphore.Release();
                }
            }
        });
    }

    private enum FileEventType
    {
        Created,
        Changed
    }

    /// <summary>
    /// Unified handler for Created and Changed events with proper debouncing.
    /// Both events fire when copying a file, so we need to handle them identically.
    /// </summary>
    private void HandleFileEventAsync(string filePath, FileEventType eventType)
    {
        if (!ShouldProcessChange(filePath))
        {
            _logger.LogDebug("Skipping duplicate {EventType} event for {FileName}", eventType, Path.GetFileName(filePath));
            return;
        }

        var fileName = Path.GetFileName(filePath);
        _logger.LogInformation("Plugin file {EventType}: {FileName}", eventType, fileName);

        _ = Task.Run(async () =>
        {
            var semaphore = GetOrCreateFileLock(filePath);

            // Try to acquire the lock - if we can't get it immediately, another operation is in progress
            if (!await semaphore.WaitAsync(0))
            {
                _logger.LogDebug("Skipping {EventType} for {FileName} - operation already in progress", eventType, fileName);
                return;
            }

            try
            {
                // Let file system settle
                await Task.Delay(200);

                // Check if file still exists (might have been deleted)
                if (!File.Exists(filePath))
                {
                    _logger.LogDebug("File no longer exists: {FileName}", fileName);
                    return;
                }

                // Check if plugin is already loaded
                bool isAlreadyLoaded;
                lock (_lock)
                {
                    isAlreadyLoaded = _plugins.ContainsKey(filePath);
                }

                if (isAlreadyLoaded)
                {
                    // Reload existing plugin
                    await UnloadPluginAsync(filePath);
                }

                await LoadPluginAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling {EventType} for plugin {FileName}", eventType, fileName);
            }
            finally
            {
                semaphore.Release();
            }
        });
    }

    private SemaphoreSlim GetOrCreateFileLock(string filePath)
    {
        lock (_fileOperationLocks)
        {
            if (!_fileOperationLocks.TryGetValue(filePath, out var semaphore))
            {
                semaphore = new SemaphoreSlim(1, 1);
                _fileOperationLocks[filePath] = semaphore;
            }
            return semaphore;
        }
    }

    private bool ShouldProcessChange(string filePath)
    {
        var now = DateTime.Now;

        lock (_lastChangeTime)
        {
            if (_lastChangeTime.TryGetValue(filePath, out var lastTime))
            {
                if (now - lastTime < _debounceInterval)
                {
                    return false;
                }
            }

            _lastChangeTime[filePath] = now;
        }

        return true;
    }

    private async Task LoadPluginAsync(string pluginPath)
    {
        var fileName = Path.GetFileName(pluginPath);

        try
        {
            var instance = new CsPluginInstance(pluginPath);

            // Create a new load context for this plugin
            instance.LoadContext = new CsPluginLoadContext();
            instance.ContextWeakRef = new WeakReference(instance.LoadContext);

            // Compile and load the plugin
            var assembly = _compiler.CompileFromFile(pluginPath, instance.LoadContext);

            // Find the IPluginV2 implementation
            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t =>
                    t is { IsInterface: false, IsAbstract: false } &&
                    t.GetInterface(nameof(IPluginV2)) != null);

            if (pluginType == null)
            {
                var availableTypes = string.Join(", ", assembly.GetTypes().Select(t => t.FullName));
                throw new InvalidOperationException(
                    $"No IPluginV2 implementation found. Available types: {availableTypes}");
            }

            _logger.LogDebug("Found plugin type: {TypeName}", pluginType.FullName);

            // Create a scoped service provider for this plugin
            instance.PluginServiceProvider = CreatePluginScopedProvider(pluginType, _rootServiceProvider);

            // Instantiate the plugin using the scoped provider
            instance.Plugin = (IPluginV2)ActivatorUtilities.CreateInstance(
                instance.PluginServiceProvider,
                pluginType);

            lock (_lock)
            {
                _plugins[pluginPath] = instance;
            }

            _logger.LogInformation("[{FileName}] Loaded: {PluginName} v{Version} by {Author}",
                fileName, instance.Plugin.Name, instance.Plugin.Version, instance.Plugin.Author);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{FileName}] Failed to load", fileName);
            throw;
        }
    }

    private async Task UnloadPluginAsync(string pluginPath)
    {
        CsPluginInstance? instance;

        lock (_lock)
        {
            if (!_plugins.Remove(pluginPath, out instance))
            {
                return;
            }
        }

        var fileName = instance.FileName;
        var pluginName = instance.Plugin?.Name ?? "Unknown";

        _logger.LogInformation("[{FileName}] Unloading {PluginName}...", fileName, pluginName);

        // Dispose the instance (this calls Dispose on plugin if IDisposable, and unloads context)
        instance.Dispose();

        // Wait for GC to collect the context
        var unloaded = instance.WaitForUnload();

        if (unloaded)
        {
            _logger.LogInformation("[{FileName}] Unloaded successfully", fileName);
        }
        else
        {
            _logger.LogWarning("[{FileName}] Context may not have fully unloaded - check for lingering references",
                fileName);
        }
    }

    /// <summary>
    /// Creates a child service provider that includes the plugin's RegisterDependencies registrations.
    /// </summary>
    private IServiceProvider CreatePluginScopedProvider(Type pluginType, IServiceProvider rootProvider)
    {
        // Create a new service collection
        var services = new ServiceCollection();

        // Add all services from the root provider by creating a wrapper
        // that resolves from the root for anything not explicitly registered
        services.AddSingleton<IServiceProvider>(sp => rootProvider);

        // Try to invoke RegisterDependencies if it exists
        try
        {
            var registrationMethod = pluginType.GetMethod(nameof(IPluginV2.RegisterDependencies));
            if (registrationMethod != null)
            {
                _logger.LogDebug("Invoking RegisterDependencies for {TypeName}", pluginType.Name);
                registrationMethod.Invoke(null, [services]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invoke RegisterDependencies for {TypeName}", pluginType.Name);
        }

        // Build a provider that chains to the root provider
        return new ChainedServiceProvider(services.BuildServiceProvider(), rootProvider);
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();

        lock (_lock)
        {
            foreach (var instance in _plugins.Values)
            {
                instance.Dispose();
            }

            _plugins.Clear();
        }
    }
}

/// <summary>
/// A service provider that tries the child provider first, then falls back to the parent.
/// </summary>
internal class ChainedServiceProvider : IServiceProvider
{
    private readonly IServiceProvider _child;
    private readonly IServiceProvider _parent;

    public ChainedServiceProvider(IServiceProvider child, IServiceProvider parent)
    {
        _child = child;
        _parent = parent;
    }

    public object? GetService(Type serviceType)
    {
        // Try child first
        var service = _child.GetService(serviceType);
        if (service != null)
        {
            return service;
        }

        // Fall back to parent
        return _parent.GetService(serviceType);
    }
}
