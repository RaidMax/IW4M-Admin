using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.IO;

public class BaseConfigurationHandlerV2<TConfigurationType> : IConfigurationHandlerV2<TConfigurationType>
    where TConfigurationType : class
{
    private readonly ILogger<BaseConfigurationHandlerV2<TConfigurationType>> _logger;
    private readonly ConfigurationWatcher _watcher;

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly SemaphoreSlim _onIo = new(1, 1);
    private TConfigurationType _configurationInstance;
    private event Action<string> FileUpdated;

    public string Filename { get; private set; } = string.Empty;

    public BaseConfigurationHandlerV2(ILogger<BaseConfigurationHandlerV2<TConfigurationType>> logger,
        ConfigurationWatcher watcher)
    {
        _logger = logger;
        _watcher = watcher;
        FileUpdated += OnFileUpdated;
    }

    ~BaseConfigurationHandlerV2()
    {
        FileUpdated -= OnFileUpdated;
        _watcher.Unregister(Filename);
    }

    public async Task<TConfigurationType> Get(string configurationName,
        TConfigurationType defaultConfiguration = default)
    {
        if (string.IsNullOrWhiteSpace(configurationName))
        {
            return defaultConfiguration;
        }

        Filename = ResolveConfigurationPath(configurationName);
        WarnIfTypeNotOwnedByPlugin();
        MigrateLegacyConfiguration(configurationName, Filename);

        TConfigurationType readConfiguration = null;

        try
        {
            await _onIo.WaitAsync();
            await using var fileStream = File.OpenRead(Filename);
            readConfiguration =
                await JsonSerializer.DeserializeAsync<TConfigurationType>(fileStream, _serializerOptions);
            _watcher.Register(Filename, FileUpdated);

            if (readConfiguration is null)
            {
                _logger.LogError("Could not parse configuration {Type} at {FileName}", typeof(TConfigurationType).Name,
                    Filename);

                return defaultConfiguration;
            }
        }
        catch (FileNotFoundException)
        {
            if (defaultConfiguration is not null)
            {
                await InternalSet(defaultConfiguration, false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not read configuration file at {Path}", Filename);
            return defaultConfiguration;
        }
        finally
        {
            if (_onIo.CurrentCount == 0)
            {
                _onIo.Release(1);
            }
        }

        return _configurationInstance ??= readConfiguration;
    }

    public async Task Set(TConfigurationType configuration)
    {
        await InternalSet(configuration, true);
    }

    public async Task Set()
    {
        if (_configurationInstance is not null)
        {
            await InternalSet(_configurationInstance, true);
        }
    }

    public event Action<TConfigurationType> Updated;

    private async Task InternalSet(TConfigurationType configuration, bool awaitSemaphore)
    {
        try
        {
            if (awaitSemaphore)
            {
                await _onIo.WaitAsync();
            }

            await using var fileStream = File.Create(Filename);
            await JsonSerializer.SerializeAsync(fileStream, configuration, _serializerOptions);
            _configurationInstance = configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not save configuration {Type} {Path}", configuration.GetType().Name, Filename);
        }
        finally
        {
            if (awaitSemaphore && _onIo.CurrentCount == 0)
            {
                _onIo.Release(1);
            }
        }
    }

    private async void OnFileUpdated(string filePath)
    {
        try
        {
            await _onIo.WaitAsync();

            try
            {
                await using var fileStream = new FileStream(Filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var readConfiguration =
                    await JsonSerializer.DeserializeAsync<TConfigurationType>(fileStream, _serializerOptions);

                if (readConfiguration is null)
                {
                    _logger.LogWarning("Could not parse updated configuration {Type} at {Path} - deserialization returned null",
                        typeof(TConfigurationType).Name, filePath);
                    return;
                }

                CopyUpdatedProperties(readConfiguration);
                Updated?.Invoke(readConfiguration);
            }
            finally
            {
                if (_onIo.CurrentCount == 0)
                {
                    _onIo.Release(1);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse updated configuration {Type} at {Path}",
                typeof(TConfigurationType).Name, filePath);
        }
    }

    private void CopyUpdatedProperties(TConfigurationType newConfiguration)
    {
        if (_configurationInstance is null)
        {
            _configurationInstance = newConfiguration;
            return;
        }

        _logger.LogDebug("Updating existing config with new values {Type} at {Path}", typeof(TConfigurationType).Name,
            Filename);

        if (_configurationInstance is IDictionary configDict && newConfiguration is IDictionary newConfigDict)
        {
            configDict.Clear();
            foreach (var key in newConfigDict.Keys)
            {
                configDict.Add(key, newConfigDict[key]);
            }
        }
        else
        {
            foreach (var property in _configurationInstance.GetType().GetProperties()
                         .Where(prop => prop.CanRead && prop.CanWrite))
            {
                property.SetValue(_configurationInstance, property.GetValue(newConfiguration));
            }
        }
    }

    /// <summary>
    /// Resolves the on-disk path for this configuration. Types defined in a host/framework assembly
    /// keep the legacy flat <c>Configuration/</c> location; everything else (a plugin, in any load
    /// format) routes to its own <c>Plugins/&lt;key&gt;/</c> folder. The configuration name may contain
    /// forward-slash segments to nest the file in a subfolder; segments are sanitized and the result is
    /// constrained to the resolved root.
    /// </summary>
    private static string ResolveConfigurationPath(string configurationName)
    {
        var assembly = typeof(TConfigurationType).Assembly;
        var root = PluginDirectoryResolver.IsHostAssembly(assembly)
            ? Path.Join(Utilities.OperatingDirectory, "Configuration")
            : PluginDirectoryResolver.ResolveDirectory(assembly);

        return PluginDirectoryResolver.CombineWithinRoot(root, configurationName, ".json");
    }

    // warns at most once per closed configuration type (this static is per generic instantiation)
    private static bool _warnedNotOwnedByPlugin;

    /// <summary>
    /// Warns (but does not block) when this configuration type is defined outside any real plugin — e.g.
    /// in a shared helper library referenced by several plugins. In that case the folder key is the shared
    /// library, so multiple plugins would share one folder. Host/framework types and types that live in a
    /// genuine plugin assembly never warn.
    /// </summary>
    private void WarnIfTypeNotOwnedByPlugin()
    {
        if (_warnedNotOwnedByPlugin)
        {
            return;
        }

        var assembly = typeof(TConfigurationType).Assembly;
        if (PluginDirectoryResolver.IsHostAssembly(assembly) ||
            PluginDirectoryResolver.LooksLikePluginAssembly(assembly))
        {
            return;
        }

        _warnedNotOwnedByPlugin = true;
        _logger.LogWarning(
            "Configuration type {Type} is defined in assembly {Assembly}, which does not contain a plugin. " +
            "It will use the data folder Plugins/{Key}/, shared by any plugin that references this assembly. " +
            "Define configuration types inside your own plugin to keep its data folder isolated.",
            typeof(TConfigurationType).Name, assembly.GetName().Name, PluginDirectoryResolver.ResolveKey(assembly));
    }

    /// <summary>
    /// One-time relocation of a plugin configuration from the legacy shared <c>Configuration/</c> folder
    /// into the plugin's own folder. Runs only for plugin configs, only when the target is absent and the
    /// legacy file is present — so it is idempotent and cannot double-apply across restarts.
    ///
    /// This lives on the handler rather than in <c>ConfigurationMigration</c> deliberately: the migration
    /// needs the <typeparamref name="TConfigurationType"/> → plugin-folder mapping to know which plugin a
    /// flat <c>Configuration/*.json</c> belongs to, and that type context only exists here at
    /// config-resolution time. The startup <c>ConfigurationMigration</c> sweep has no such per-type context.
    /// </summary>
    private void MigrateLegacyConfiguration(string configurationName, string targetPath)
    {
        if (PluginDirectoryResolver.IsHostAssembly(typeof(TConfigurationType).Assembly))
        {
            return; // host configs already live in Configuration/
        }

        if (File.Exists(targetPath))
        {
            return; // already in the plugin folder (or already migrated)
        }

        var flatName = configurationName.Replace("\\", string.Empty).Replace("/", string.Empty);
        var legacyPath = Path.Join(Utilities.OperatingDirectory, "Configuration", $"{flatName}.json");

        if (!File.Exists(legacyPath))
        {
            return;
        }

        try
        {
            File.Move(legacyPath, targetPath);
            _logger.LogInformation(
                "Migrated plugin configuration {Name} from {OldPath} to {NewPath}", flatName, legacyPath,
                targetPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not migrate legacy configuration {Name} to {NewPath}", flatName,
                targetPath);
        }
    }
}
