using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using IW4MAdmin.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
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
            if (_fileOperationLocks.TryGetValue(filePath, out var semaphore))
            {
                // Check if semaphore was disposed (race condition protection)
                try
                {
                    // Try to access a property to verify it's not disposed
                    _ = semaphore.CurrentCount;
                    return semaphore;
                }
                catch (ObjectDisposedException)
                {
                    // Semaphore was disposed, create a new one
                    _fileOperationLocks.Remove(filePath);
                }
            }

            semaphore = new SemaphoreSlim(1, 1);
            _fileOperationLocks[filePath] = semaphore;
            return semaphore;
        }
    }

    private bool ShouldProcessChange(string filePath)
    {
        var now = DateTime.UtcNow;

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

            // CRITICAL: Get plugin name BEFORE creating service provider and instantiating
            // We need the name available during plugin construction so config reads work
            // Read the Name property via reflection (no instantiation needed for property getters)
            string? pluginName = null;
            try
            {
                // Get the Name property and read it via reflection
                // This works because Name is typically an expression-bodied property or simple getter
                var nameProperty = pluginType.GetProperty("Name");
                if (nameProperty != null && nameProperty.CanRead)
                {
                    // Try to get a default instance if possible (for expression-bodied properties)
                    // If that fails, we'll try to compile/evaluate the property getter
                    object? tempInstance = null;
                    try
                    {
                        tempInstance = Activator.CreateInstance(pluginType);
                    }
                    catch
                    {
                        // Can't create instance - will try other approaches
                    }
                    
                    if (tempInstance != null)
                    {
                        pluginName = nameProperty.GetValue(tempInstance)?.ToString();
                    }
                }
                
                if (!string.IsNullOrEmpty(pluginName))
                {
                    instance.PluginName = pluginName;
                }
            }
            catch (Exception)
            {
                // Reflection failed - we'll get name after instantiation
            }

            // Create a plugin-scoped provider that can resolve both plugin services and root services
            // Use a closure that will have plugin name available if we got it above
            instance.PluginServiceProvider = CreatePluginScopedProvider(pluginType, () => 
            {
                // Use cached name if we got it, otherwise try instance.Plugin.Name (available during construction in some cases)
                var name = !string.IsNullOrEmpty(instance.PluginName) ? instance.PluginName : (instance.Plugin?.Name);
                return name ?? string.Empty;
            });

            // Set plugin name on wrapper BEFORE instantiation if we got it
            if (!string.IsNullOrEmpty(pluginName))
            {
                var configWrapperBeforeInit = instance.PluginServiceProvider.GetService<ICsScriptPluginConfiguration>();
                if (configWrapperBeforeInit != null)
                {
                    configWrapperBeforeInit.SetName(pluginName);
                }
            }

            // Now instantiate the plugin using the scoped provider (this will run the constructor)
            instance.Plugin = (IPluginV2)ActivatorUtilities.CreateInstance(
                instance.PluginServiceProvider,
                pluginType);

            // Update plugin name if we didn't have it before, and ensure wrapper has it
            if (string.IsNullOrEmpty(instance.PluginName))
            {
                instance.PluginName = instance.Plugin.Name;
            }
            
            // Ensure configuration wrapper has the plugin name
            var configWrapper = instance.PluginServiceProvider.GetService<ICsScriptPluginConfiguration>();
            if (configWrapper != null)
            {
                // Always set it in case it wasn't set before, or to update if we just got the name
                configWrapper.SetName(instance.PluginName);
                _logger.LogDebug("[{FileName}] Set plugin name '{PluginName}' on configuration wrapper", fileName, instance.PluginName);
            }

            // Discover and register commands from the plugin assembly
            await RegisterPluginCommands(instance, assembly);

            lock (_lock)
            {
                _plugins[pluginPath] = instance;
            }

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
        CsPluginInstance? instance;

        lock (_lock)
        {
            if (!_plugins.Remove(pluginPath, out instance))
            {
                return;
            }
        }

        var fileName = instance?.FileName;
        var pluginName = instance?.Plugin?.Name ?? "Unknown";

        _logger.LogInformation("[{FileName}] Unloading {PluginName}...", fileName, pluginName);

        // Unregister commands before disposing
        if (instance != null)
        {
            UnregisterPluginCommands(instance);
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

        // Clean up file operation lock semaphore
        lock (_fileOperationLocks)
        {
            if (_fileOperationLocks.TryGetValue(pluginPath, out var semaphore))
            {
                try
                {
                    semaphore.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Semaphore already disposed, ignore
                }
                _fileOperationLocks.Remove(pluginPath);
            }
        }

        // Clean up change tracking entry
        lock (_lastChangeTime)
        {
            _lastChangeTime.Remove(pluginPath);
        }
    }

    /// <summary>
    /// Creates a plugin-scoped service provider that can resolve:
    /// 1. Services registered by the plugin's RegisterDependencies (built with root provider for their deps)
    /// 2. All services from the root provider
    /// 3. ICsScriptPluginConfiguration wrapper for shared ScriptPluginSettings.json access
    /// </summary>
    private IServiceProvider CreatePluginScopedProvider(Type pluginType, Func<string?> pluginNameProvider)
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
                    _logger.LogWarning("RegisterDependencies for {TypeName} has invalid signature. Expected: static void RegisterDependencies(IServiceCollection)", pluginType.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invoke RegisterDependencies for {TypeName}", pluginType.Name);
        }

        // Register the shared configuration wrapper for ScriptPluginSettings.json
        // This allows plugins to opt-in by requesting ICsScriptPluginConfiguration in their constructor
        pluginServices.AddSingleton<ICsScriptPluginConfiguration>(serviceProvider =>
        {
            var configHandler = _rootServiceProvider.GetRequiredService<IConfigurationHandlerV2<ScriptPluginConfiguration>>();
            var wrapperLogger = _rootServiceProvider.GetRequiredService<ILogger<CsScriptPluginConfigurationWrapper>>();
            // Pass the plugin name provider so GetValue can use it even before SetName is called
            var wrapper = new CsScriptPluginConfigurationWrapper(configHandler, wrapperLogger, pluginNameProvider);
            
            // Set plugin name if available (for immediate use)
            var pluginName = pluginNameProvider();
            if (!string.IsNullOrEmpty(pluginName))
            {
                wrapper.SetName(pluginName);
            }
            
            return wrapper;
        });

        // Build a composite provider that:
        // 1. First tries to resolve from plugin-registered services (instantiated with root provider)
        // 2. Falls back to root provider for everything else
        return new PluginScopedServiceProvider(pluginServices, _rootServiceProvider, _logger);
    }

    /// <summary>
    /// Discovers Command classes from the plugin assembly and registers them with the manager.
    /// </summary>
    private async Task RegisterPluginCommands(CsPluginInstance instance, System.Reflection.Assembly assembly)
    {
        try
        {
            // Get manager and required services for command instantiation
            var manager = _rootServiceProvider.GetRequiredService<IManager>();
            var commandConfig = _rootServiceProvider.GetRequiredService<CommandConfiguration>();
            var translationLookup = _rootServiceProvider.GetRequiredService<ITranslationLookup>();

            // Discover all Command classes in the assembly
            var commandTypes = assembly.GetTypes()
                .Where(type =>
                    type.IsClass &&
                    !type.IsAbstract &&
                    type.BaseType == typeof(Command) &&
                    (type.Namespace == null || !type.Namespace.StartsWith(nameof(SharedLibraryCore))))
                .ToList();

            // Before registering, check for any orphaned commands from previous plugin instances
            // that might have the same names/aliases but weren't properly cleaned up
            foreach (var commandType in commandTypes)
            {
                try
                {
                    // Create a temporary command instance to get its name/alias
                    var tempCommand = (Command)ActivatorUtilities.CreateInstance(
                        _rootServiceProvider,
                        commandType,
                        commandConfig,
                        translationLookup);
                    
                    var commandName = tempCommand.Name;
                    var commandAlias = tempCommand.Alias;
                    
                    // Check if a command with this name or alias already exists
                    var existingCommands = manager.GetCommands().Where(cmd =>
                        cmd.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(commandAlias) && cmd.Alias?.Equals(commandAlias, StringComparison.OrdinalIgnoreCase) == true) ||
                        (!string.IsNullOrEmpty(cmd.Alias) && cmd.Alias.Equals(commandName, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(commandAlias) && cmd.Name.Equals(commandAlias, StringComparison.OrdinalIgnoreCase)) ||
                        cmd.GetType().Name == commandType.Name).ToList();
                    
                    if (existingCommands.Count > 0)
                    {
                        _logger.LogDebug("[{FileName}] Removing {Count} existing command(s) matching {CommandType} before registration",
                            instance.FileName, existingCommands.Count, commandType.Name);
                        
                        foreach (var existingCmd in existingCommands)
                        {
                            manager.RemoveCommandByName(existingCmd.Name);
                        }
                        
                        // Small delay to ensure cleanup
                        await Task.Delay(50);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[{FileName}] Error during pre-cleanup check for {CommandType}",
                        instance.FileName, commandType.Name);
                }
            }

            foreach (var commandType in commandTypes)
            {
                try
                {
                    // Check if command already exists before attempting to register
                    // This can happen during hot reload if the old command wasn't fully removed yet
                    var commandName = commandType.Name.Replace("Command", "").ToLower();
                    var existingCommand = manager.GetCommands()
                        .FirstOrDefault(cmd => 
                            cmd.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase) ||
                            cmd.GetType() == commandType);

                    if (existingCommand != null)
                    {
                        // Try to remove the existing command first (might be from a previous plugin instance)
                        manager.RemoveCommandByName(existingCommand.Name);
                        await Task.Delay(10);
                    }

                    // Instantiate the command using ActivatorUtilities (supports constructor injection)
                    // Commands need CommandConfiguration and ITranslationLookup as constructor parameters
                    var command = (Command)ActivatorUtilities.CreateInstance(
                        _rootServiceProvider,
                        commandType,
                        commandConfig,
                        translationLookup);

                    // Register with manager
                    manager.AddAdditionalCommand(command);
                    instance.RegisteredCommands.Add(command);

                    _logger.LogDebug("[{FileName}] Registered command: {CommandName} (alias: {Alias})",
                        instance.FileName, command.Name, command.Alias ?? "none");
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Duplicate command"))
                {
                    // Command with same name/alias already exists - try to remove and retry once
                    _logger.LogWarning("[{FileName}] Duplicate command detected: {CommandType}. Attempting cleanup...",
                        instance.FileName, commandType.Name);
                    
                    try
                    {
                        // First, instantiate to get the actual command name and alias
                        var testCommand = (Command)ActivatorUtilities.CreateInstance(
                            _rootServiceProvider,
                            commandType,
                            commandConfig,
                            translationLookup);
                        
                        var commandName = testCommand.Name;
                        var commandAlias = testCommand.Alias;
                        
                        // Find ALL matching commands (by name, alias, or type)
                        var allCommands = manager.GetCommands().ToList();
                        var toRemove = allCommands.Where(cmd => 
                            cmd.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrEmpty(commandAlias) && cmd.Alias?.Equals(commandAlias, StringComparison.OrdinalIgnoreCase) == true) ||
                            cmd.GetType().Name == commandType.Name ||
                            (!string.IsNullOrEmpty(cmd.Alias) && cmd.Alias.Equals(commandName, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(commandAlias) && cmd.Name.Equals(commandAlias, StringComparison.OrdinalIgnoreCase))).ToList();
                        
                        // Remove all matching commands
                        foreach (var cmdToRemove in toRemove)
                        {
                            manager.RemoveCommandByName(cmdToRemove.Name);
                        }
                        
                        // Verify removal worked and retry if needed
                        await Task.Delay(100);
                        var remainingCommands = manager.GetCommands().Where(cmd => 
                            cmd.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrEmpty(commandAlias) && cmd.Alias?.Equals(commandAlias, StringComparison.OrdinalIgnoreCase) == true)).ToList();
                        
                        if (remainingCommands.Count > 0)
                        {
                            // Force remove any remaining duplicates
                            foreach (var remainingCmd in remainingCommands)
                            {
                                manager.RemoveCommandByName(remainingCmd.Name);
                            }
                            await Task.Delay(50);
                        }
                        
                        // Retry registration with the already-instantiated command
                        manager.AddAdditionalCommand(testCommand);
                        instance.RegisteredCommands.Add(testCommand);
                        
                        _logger.LogDebug("[{FileName}] Successfully registered command after cleanup: {CommandName}",
                            instance.FileName, testCommand.Name);
                    }
                    catch (Exception retryEx)
                    {
                        _logger.LogError(retryEx, "[{FileName}] Failed to register command after cleanup attempt: {CommandType}",
                            instance.FileName, commandType.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{FileName}] Failed to register command: {CommandType}",
                        instance.FileName, commandType.Name);
                }
            }

            if (instance.RegisteredCommands.Count > 0)
            {
                _logger.LogDebug("[{FileName}] Registered {Count} command(s)",
                    instance.FileName, instance.RegisteredCommands.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{FileName}] Error discovering/registering commands",
                instance.FileName);
        }
    }

    /// <summary>
    /// Unregisters all commands that were registered by this plugin instance.
    /// </summary>
    private void UnregisterPluginCommands(CsPluginInstance instance)
    {
        try
        {
            var manager = _rootServiceProvider.GetRequiredService<IManager>();
            var commandCount = instance.RegisteredCommands.Count;

            if (commandCount == 0)
            {
                _logger.LogDebug("[{FileName}] No commands to unregister", instance.FileName);
                return;
            }

            var unregisteredCount = 0;
            var commandsToRemove = new List<IManagerCommand>(instance.RegisteredCommands);
            
            foreach (var command in commandsToRemove)
            {
                try
                {
                    // Remove by name
                    manager.RemoveCommandByName(command.Name);
                    unregisteredCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[{FileName}] Failed to unregister command: {CommandName}",
                        instance.FileName, command.Name);
                }
            }

            instance.RegisteredCommands.Clear();
            
            if (unregisteredCount > 0)
            {
                _logger.LogDebug("[{FileName}] Unregistered {Count} command(s)", instance.FileName, unregisteredCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{FileName}] Error unregistering commands",
                instance.FileName);
        }
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();

        foreach (var instance in _plugins.Values)
        {
            instance.Dispose();
        }

        _plugins.Clear();

        foreach (var semaphore in _fileOperationLocks.Values)
        {
            semaphore.Dispose();
        }

        _fileOperationLocks.Clear();
    }
}
