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
using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.RConParsers;

#nullable enable

namespace IW4MAdmin.Application.Plugin.CSharpScript;

/// <summary>
/// Manages the lifecycle of dynamically loaded .cs plugins with hot reload support.
/// Watches the Plugins directory for .cs file changes and automatically reloads.
/// </summary>
public class CsPluginServiceHost : ICsPluginServiceHost
{
    private readonly ConcurrentDictionary<string, CsPluginInstance> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<string>> _parserFilesByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IRConParser> _loadedRConParsers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IEventParser> _loadedEventParsers = new(StringComparer.Ordinal);
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
                .Select(p => p.Plugin)
                .ToList();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IRConParser> LoadedRConParsers => _loadedRConParsers.Values.ToList();

    /// <inheritdoc />
    public IReadOnlyList<IEventParser> LoadedEventParsers => _loadedEventParsers.Values.ToList();

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

    private async Task OnFileChanged(string filePath)
    {
        // Check if plugin is already loaded
        var isAlreadyLoaded = _plugins.ContainsKey(filePath) || _parserFilesByPath.ContainsKey(filePath);

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
            await LoadPluginAsync(newPath);
        }
    }

    private async Task LoadPluginAsync(string pluginPath)
    {
        var fileName = Path.GetFileName(pluginPath);
        CsPluginInstance? instance = null;

        try
        {
            var loadContext = new CsPluginLoadContext();
            var assembly = await _compiler.CompileFromFile(pluginPath, loadContext);

            var loadableTypes = assembly.GetTypes()
                .Where(t => t is { IsInterface: false, IsAbstract: false })
                .ToList();

            // A .cs file may contribute a plugin (IPluginV2), one or more parser
            // definitions (IParserDefinition), or both. The host materializes parser
            // definitions into concrete parsers it owns; the manager projects them.
            var pluginType = loadableTypes
                .FirstOrDefault(t => t.GetInterface(nameof(IPluginV2)) != null);
            var parserTypes = loadableTypes
                .Where(t => t.GetInterface(nameof(IParserDefinition)) != null)
                .ToList();

            if (pluginType is null && parserTypes.Count == 0)
            {
                // Not necessarily an error: the file may be a work-in-progress that does not yet
                // implement either contract. Skip it rather than crashing the load pass.
                var availableTypes = string.Join(", ", assembly.GetTypes().Select(t => t.FullName));
                _logger.LogWarning(
                    "[{FileName}] No IPluginV2 or IParserDefinition implementation found; skipping. Available types: {AvailableTypes}",
                    fileName, availableTypes);
                return;
            }

            if (parserTypes.Count > 0)
            {
                LoadParsers(pluginPath, fileName, parserTypes);
            }

            // Parser-only file: nothing more to do. The parser configuration is copied into
            // host-owned parser instances, so the plugin assembly does not need to stay loaded.
            if (pluginType is null)
            {
                return;
            }

            _logger.LogDebug("Found plugin type: {TypeName}", pluginType.FullName);

            var scopedProvider = CreatePluginScopedProvider(pluginType);

            instance = new CsPluginInstance(pluginPath)
            {
                LoadContext = loadContext,
                PluginServiceProvider = scopedProvider,
                Plugin = (IPluginV2)ActivatorUtilities.CreateInstance(scopedProvider, pluginType)
            };

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
        if (_parserFilesByPath.TryRemove(pluginPath, out var parserNames))
        {
            foreach (var name in parserNames)
            {
                _loadedRConParsers.TryRemove(name, out _);
                _loadedEventParsers.TryRemove(name, out _);
                _logger.LogInformation("[{FileName}] Unloaded parser: {ParserName}",
                    Path.GetFileName(pluginPath), name);
            }
        }

        if (!_plugins.TryRemove(pluginPath, out var instance))
        {
            return;
        }

        var fileName = instance.FileName;
        var pluginName = instance.Plugin.Name ?? "Unknown";

        _logger.LogInformation("[{FileName}] Unloading {PluginName}...", fileName, pluginName);

        // Unregister commands before disposing
        _commandRegistrar.UnregisterCommands(instance);

        // Dispose the instance (this calls Dispose on plugin if IDisposable, and unloads context)
        instance.Dispose();

        // Wait for GC to collect the context
        var unloaded = await instance.WaitForUnloadAsync();

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
    /// Materializes each parser definition into a concrete RCon/event parser the host owns,
    /// replacing any parser already loaded under the same name. The manager projects these via
    /// <see cref="LoadedRConParsers" />/<see cref="LoadedEventParsers" />; the host never mutates
    /// manager state directly.
    /// </summary>
    private void LoadParsers(string pluginPath, string fileName, IEnumerable<Type> parserTypes)
    {
        var loadedNames = new List<string>();

        foreach (var parserType in parserTypes)
        {
            var definition = (IParserDefinition)ActivatorUtilities.CreateInstance(_rootServiceProvider, parserType);

            var rconParser = ActivatorUtilities.CreateInstance<DynamicRConParser>(_rootServiceProvider);
            var eventParser = ActivatorUtilities.CreateInstance<DynamicEventParser>(_rootServiceProvider);
            rconParser.Name = definition.Name;
            eventParser.Name = definition.Name;
            definition.Configure(rconParser, eventParser);

            _loadedRConParsers[definition.Name] = rconParser;
            _loadedEventParsers[definition.Name] = eventParser;

            loadedNames.Add(definition.Name);
            _logger.LogInformation("[{FileName}] Loaded parser: {ParserName}", fileName, definition.Name);
        }

        _parserFilesByPath[pluginPath] = loadedNames;
    }

    /// <summary>
    /// Creates a plugin-scoped service provider that can resolve:
    /// 1. Services registered by the plugin's RegisterDependencies (built with root provider for their deps)
    /// 2. All services from the root provider
    /// </summary>
    private PluginScopedServiceProvider CreatePluginScopedProvider(Type pluginType)
    {
        // Collect the plugin's service registrations
        var pluginServices = new ServiceCollection();

        var registrationMethod = pluginType.GetMethod(
            nameof(IPluginV2.RegisterDependencies),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        if (registrationMethod is null)
        {
            return new PluginScopedServiceProvider(pluginServices, _rootServiceProvider, _logger);
        }

        // Validate method signature
        var parameters = registrationMethod.GetParameters();
        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(IServiceCollection))
        {
            // if this fails we want the developer to be informed immediately instead
            // of runtime DI inconsistencies 
            _logger.LogDebug("Invoking RegisterDependencies for {TypeName}", pluginType.Name);
            registrationMethod.Invoke(null, [pluginServices]);
        }
        else
        {
            _logger.LogWarning(
                "RegisterDependencies for {TypeName} has invalid signature. Expected: static void RegisterDependencies(IServiceCollection)",
                pluginType.Name);
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
