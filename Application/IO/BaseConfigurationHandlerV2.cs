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
    private string _path = string.Empty;
    private event Action<string> FileUpdated;

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
        _watcher.Unregister(_path);
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

        _path = Path.Join(Utilities.OperatingDirectory, "Configuration", $"{cleanName}.json");
        TConfigurationType readConfiguration = null;

        try
        {
            await _onIo.WaitAsync();
            await using var fileStream = File.OpenRead(_path);
            readConfiguration =
                await JsonSerializer.DeserializeAsync<TConfigurationType>(fileStream, _serializerOptions);
            await fileStream.DisposeAsync();
            _watcher.Register(_path, FileUpdated);

            if (readConfiguration is null)
            {
                _logger.LogError("Could not parse configuration {Type} at {FileName}", typeof(TConfigurationType).Name,
                    _path);

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
            _logger.LogError(ex, "Could not read configuration file at {Path}", _path);
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

            await using var fileStream = File.Create(_path);
            await JsonSerializer.SerializeAsync(fileStream, configuration, _serializerOptions);
            await fileStream.DisposeAsync();
            _configurationInstance = configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not save configuration {Type} {Path}", configuration.GetType().Name, _path);
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
            await using var fileStream = File.OpenRead(_path);
            var readConfiguration =
                await JsonSerializer.DeserializeAsync<TConfigurationType>(fileStream, _serializerOptions);
            await fileStream.DisposeAsync();

            if (readConfiguration is null)
            {
                _logger.LogWarning("Could not parse updated configuration {Type} at {Path}",
                    typeof(TConfigurationType).Name, filePath);
            }
            else
            {
                CopyUpdatedProperties(readConfiguration);
                Updated?.Invoke(readConfiguration);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse updated configuration {Type} at {Path}",
                typeof(TConfigurationType).Name, filePath);
        }
        finally
        {
            if (_onIo.CurrentCount == 0)
            {
                _onIo.Release(1);
            }
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
            _path);

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
