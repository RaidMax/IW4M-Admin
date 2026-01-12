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

        var cleanName = configurationName.Replace("\\", "").Replace("/", "");

        if (string.IsNullOrWhiteSpace(configurationName))
        {
            return defaultConfiguration;
        }

        Filename = Path.Join(Utilities.OperatingDirectory, "Configuration", $"{cleanName}.json");
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
}
