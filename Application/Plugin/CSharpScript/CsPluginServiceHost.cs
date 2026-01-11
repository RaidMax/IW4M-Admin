using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    private readonly ConcurrentDictionary<string, CsPluginInstance> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly CsPluginCompiler _compiler;
    private readonly CsPluginFileWatcher _fileWatcher;
    private readonly CsPluginCommandRegistrar _commandRegistrar;
    private readonly IServiceProvider _rootServiceProvider;
    private readonly ILogger<CsPluginServiceHost> _logger;

    public CsPluginServiceHost(
        IServiceProvider serviceProvider,
        CsPluginCompiler compiler,
        CsPluginFileWatcher fileWatcher,
        CsPluginCommandRegistrar commandRegistrar,
        ILogger<CsPluginServiceHost> logger)
    {
        _rootServiceProvider = serviceProvider;
        _compiler = compiler;
        _fileWatcher = fileWatcher;
        _commandRegistrar = commandRegistrar;
        _logger = logger;

        _fileWatcher.FileChanged += OnFileChanged;
        _fileWatcher.FileDeleted += OnFileDeleted;
        _fileWatcher.FileRenamed += OnFileRenamed;
    }

    /// <inheritdoc />
    public IEnumerable<IPluginV2> LoadedPlugins
    {
        get
        {
            return _plugins.Values
                .Where(p => p.Plugin != null)
                .Select(p => p.Plugin!)
                .ToList();
        }
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting C# script plugin service host");
        _logger.LogInformation("Plugins directory: {Directory}", Utilities.PluginsDirectory);

        // Load all existing .cs plugins
        var pluginFiles = Directory.GetFiles(Utilities.PluginsDirectory, "*.cs");

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
        _fileWatcher.Start();
    }

    private async Task OnFileChanged(string filePath, FileEventType eventType)
    {
        // Check if plugin is already loaded
        var isAlreadyLoaded = _plugins.ContainsKey(filePath);

        if (isAlreadyLoaded)
        {
            // Reload existing plugin
            await UnloadPluginAsync(filePath);
        }

        await LoadPluginAsync(filePath);
    }

    private async Task OnFileDeleted(string filePath)
    {
        await UnloadPluginAsync(filePath);
    }

    private async Task OnFileRenamed(string oldPath, string newPath)
    {
        await UnloadPluginAsync(oldPath);

        // Load with new name if it's still a .cs file
        if (newPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(100);
            await LoadPluginAsync(newPath);
        }
    }

    private async Task LoadPluginAsync(string pluginPath)
    {
        var fileName = Path.GetFileName(pluginPath);
        CsPluginInstance? instance = null;

        try
        {
            instance = new CsPluginInstance(pluginPath);

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

            // Create a plugin-scoped provider that can resolve both plugin services and root services
            instance.PluginServiceProvider = CreatePluginScopedProvider(pluginType);
            ArgumentNullException.ThrowIfNull(instance.PluginServiceProvider);

            // Instantiate the plugin using the scoped provider
            instance.Plugin = (IPluginV2)ActivatorUtilities.CreateInstance(
                instance.PluginServiceProvider,
                pluginType);

            // Discover and register commands from the plugin assembly
            _commandRegistrar.RegisterCommands(instance, assembly);

            _plugins[pluginPath] = instance;

            _logger.LogInformation("[{FileName}] Loaded: {PluginName} v{Version} by {Author}",
                fileName, instance.Plugin.Name, instance.Plugin.Version, instance.Plugin.Author);
        }
        catch (Exception ex)
        {
            // Clean up instance if partially created
            instance?.Dispose();
            _logger.LogError(ex, "[{FileName}] Failed to load", fileName);
            throw;
        }
    }

    private async Task UnloadPluginAsync(string pluginPath)
    {
        if (!_plugins.TryRemove(pluginPath, out var instance))
        {
            return;
        }

        var fileName = instance?.FileName;
        var pluginName = instance?.Plugin?.Name ?? "Unknown";

        _logger.LogInformation("[{FileName}] Unloading {PluginName}...", fileName, pluginName);

        // Unregister commands before disposing
        if (instance != null)
        {
            _commandRegistrar.UnregisterCommands(instance);
        }

        // Dispose the instance (this calls Dispose on plugin if IDisposable, and unloads context)
        instance?.Dispose();

        // Wait for GC to collect the context
        var unloaded = instance != null && await instance.WaitForUnloadAsync();

        if (unloaded)
        {
            _logger.LogInformation("[{FileName}] Unloaded successfully", fileName);
        }
        else
        {
            _logger.LogWarning("[{FileName}] Context may not have fully unloaded - check for lingering references",
                fileName);
        }

        // Clean up file watcher tracking state
        _fileWatcher.CleanupFilePath(pluginPath);
    }

    /// <summary>
    /// Creates a plugin-scoped service provider that can resolve:
    /// 1. Services registered by the plugin's RegisterDependencies (built with root provider for their deps)
    /// 2. All services from the root provider
    /// </summary>
    private IServiceProvider CreatePluginScopedProvider(Type pluginType)
    {
        // Collect the plugin's service registrations
        var pluginServices = new ServiceCollection();

        try
        {
            var registrationMethod = pluginType.GetMethod(
                nameof(IPluginV2.RegisterDependencies),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            if (registrationMethod != null)
            {
                // Validate method signature
                var parameters = registrationMethod.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(IServiceCollection))
                {
                    _logger.LogDebug("Invoking RegisterDependencies for {TypeName}", pluginType.Name);
                    registrationMethod.Invoke(null, [pluginServices]);
                }
                else
                {
                    _logger.LogWarning(
                        "RegisterDependencies for {TypeName} has invalid signature. Expected: static void RegisterDependencies(IServiceCollection)",
                        pluginType.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invoke RegisterDependencies for {TypeName}", pluginType.Name);
        }

        // Build a composite provider that:
        // 1. First tries to resolve from plugin-registered services (instantiated with root provider)
        // 2. Falls back to root provider for everything else
        return new PluginScopedServiceProvider(pluginServices, _rootServiceProvider, _logger);
    }

    public void Dispose()
    {
        _fileWatcher.Dispose();

        foreach (var instance in _plugins.Values)
        {
            instance.Dispose();
        }

        _plugins.Clear();
    }
}
