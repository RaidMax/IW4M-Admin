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
            // definitions (IParserDefinition), or both. Parsers are not plugins: they
            // have no lifecycle and are materialized + registered with the manager here.
            var pluginType = loadableTypes
                .FirstOrDefault(t => t.GetInterface(nameof(IPluginV2)) != null);
            var parserTypes = loadableTypes
                .Where(t => t.GetInterface(nameof(IParserDefinition)) != null)
                .ToList();

            if (pluginType is null && parserTypes.Count == 0)
            {
                var availableTypes = string.Join(", ", assembly.GetTypes().Select(t => t.FullName));
                throw new InvalidOperationException(
                    $"No IPluginV2 or IParserDefinition implementation found. Available types: {availableTypes}");
            }

            if (parserTypes.Count > 0)
            {
                RegisterParsers(pluginPath, fileName, parserTypes);
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
            var manager = _rootServiceProvider.GetRequiredService<IManager>();
            foreach (var name in parserNames)
            {
                RemoveByName(manager.AdditionalRConParsers, p => p.Name, name);
                RemoveByName(manager.AdditionalEventParsers, p => p.Name, name);
                _logger.LogInformation("[{FileName}] Unregistered parser: {ParserName}",
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
    /// Materializes each parser definition into concrete RCon/event parsers via the manager and
    /// registers them, replacing any parser already registered under the same name. Parsers are
    /// not plugins — they have no lifecycle, so they are registered directly with the manager
    /// rather than tracked as plugin instances.
    /// </summary>
    private void RegisterParsers(string pluginPath, string fileName, IEnumerable<Type> parserTypes)
    {
        var manager = _rootServiceProvider.GetRequiredService<IManager>();
        var registeredNames = new List<string>();

        foreach (var parserType in parserTypes)
        {
            var definition = (IParserDefinition)ActivatorUtilities.CreateInstance(_rootServiceProvider, parserType);

            var rconParser = manager.GenerateDynamicRConParser(definition.Name);
            var eventParser = manager.GenerateDynamicEventParser(definition.Name);
            definition.Configure(rconParser, eventParser);

            AddOrReplace(manager.AdditionalRConParsers, rconParser, p => p.Name);
            AddOrReplace(manager.AdditionalEventParsers, eventParser, p => p.Name);

            registeredNames.Add(definition.Name);
            _logger.LogInformation("[{FileName}] Registered parser: {ParserName}", fileName, definition.Name);
        }

        _parserFilesByPath[pluginPath] = registeredNames;
    }

    private static void AddOrReplace<T>(IList<T> list, T item, Func<T, string> keySelector)
    {
        RemoveByName(list, keySelector, keySelector(item));
        list.Add(item);
    }

    private static void RemoveByName<T>(IList<T> list, Func<T, string> keySelector, string name)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (string.Equals(keySelector(list[i]), name, StringComparison.Ordinal))
            {
                list.RemoveAt(i);
            }
        }
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
