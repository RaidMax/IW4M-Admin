using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IW4MAdmin.Application.Configuration;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Plugin.CSharpScript;

/// <summary>
/// Implementation of ICsScriptPluginConfiguration that wraps ScriptPluginConfiguration.
/// Provides simple key-value access to plugin settings stored in ScriptPluginSettings.json.
/// </summary>
internal class CsScriptPluginConfigurationWrapper : ICsScriptPluginConfiguration
{
    private const string ConfigSectionName = "ScriptPluginSettings";
    private const int ConfigLoadTimeoutMs = 2000;
    private const int ConfigRefreshTimeoutMs = 500;
    private const StringComparison KeyComparison = StringComparison.OrdinalIgnoreCase;

    private ScriptPluginConfiguration _config;
    private readonly IConfigurationHandlerV2<ScriptPluginConfiguration> _configHandler;
    private readonly ILogger<CsScriptPluginConfigurationWrapper> _logger;
    private readonly Dictionary<string, List<Action<object>>> _updateCallbacks = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string key, object value)> _pendingWrites = [];
    private readonly Func<string?>? _pluginNameProvider;
    private readonly Lock _configLock = new();
    private string _pluginName = string.Empty;

    public CsScriptPluginConfigurationWrapper(
        IConfigurationHandlerV2<ScriptPluginConfiguration> configHandler,
        ILogger<CsScriptPluginConfigurationWrapper> logger,
        Func<string?>? pluginNameProvider = null)
    {
        _configHandler = configHandler ?? throw new ArgumentNullException(nameof(configHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pluginNameProvider = pluginNameProvider;
        
        _config = TryLoadConfig(TimeSpan.FromMilliseconds(ConfigLoadTimeoutMs));
        _configHandler.Updated += OnConfigurationUpdated;
    }

    private ScriptPluginConfiguration TryLoadConfig(TimeSpan timeout)
    {
        try
        {
            var getTask = _configHandler.Get(ConfigSectionName, new ScriptPluginConfiguration());
            if (getTask.Wait(timeout))
            {
                return getTask.Result;
            }

            _logger.LogWarning("Config load timed out during wrapper construction. Using default.");
            return new ScriptPluginConfiguration();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load config during wrapper construction. Using default.");
            return new ScriptPluginConfiguration();
        }
    }

    private ScriptPluginConfiguration GetOrLoadConfig()
    {
        lock (_configLock)
        {
            if (_config != null)
            {
                return _config;
            }

            _config = TryLoadConfig(TimeSpan.FromMilliseconds(ConfigRefreshTimeoutMs));
            return _config;
        }
    }

    private static bool TryFindKeyCaseInsensitive(Dictionary<string, object> dictionary, string key, out string actualKey)
    {
        actualKey = dictionary.Keys.FirstOrDefault(k => string.Equals(k, key, KeyComparison)) ?? string.Empty;
        return !string.IsNullOrEmpty(actualKey);
    }

    private string GetPluginName()
    {
        if (!string.IsNullOrEmpty(_pluginName))
        {
            return _pluginName;
        }

        var name = _pluginNameProvider?.Invoke();
        if (!string.IsNullOrEmpty(name))
        {
            _pluginName = name;
            return name;
        }

        return string.Empty;
    }

    private string? FindPluginNameByKey(string key)
    {
        var config = GetOrLoadConfig();
        if (config == null || config.Count == 0)
        {
            return null;
        }

        // Search through all plugin sections to find one containing the requested key
        // This is only used as a fallback when plugin name isn't available during construction
        foreach (var section in config)
        {
            var sectionConfig = section.Value;
            if (sectionConfig == null)
            {
                continue;
            }

            // Case-insensitive key lookup
            if (TryFindKeyCaseInsensitive(sectionConfig, key, out _))
            {
                // Found a section with this key - use this section as the plugin name temporarily
                // Don't cache this as _pluginName - let the proper plugin name be set later via SetName()
                return section.Key;
            }
        }

        return null;
    }

    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Plugin name cannot be null or empty", nameof(name));
        }

        _pluginName = name;
        ApplyPendingWrites();
    }

    private void ApplyPendingWrites()
    {
        if (_pendingWrites.Count == 0)
        {
            return;
        }

        List<(string key, object value)> writesToApply;
        lock (_pendingWrites)
        {
            writesToApply = new List<(string key, object value)>(_pendingWrites);
            _pendingWrites.Clear();
        }

        var currentConfig = GetOrLoadConfig();

        // Apply pending writes only if keys don't already exist (don't overwrite user settings)
        foreach (var (key, value) in writesToApply)
        {
            if (!currentConfig.TryGetValue(_pluginName, out var pluginConfig))
            {
                // Plugin section doesn't exist, safe to write
                _ = SetValueAsync(key, value);
                continue;
            }

            // Check if key exists (case-insensitive) - only write if it doesn't exist
            if (!TryFindKeyCaseInsensitive(pluginConfig, key, out _))
            {
                _ = SetValueAsync(key, value);
            }
        }
    }

    public T GetValue<T>(string key)
    {
        return GetValue(key, default(T)!);
    }

    public T GetValue<T>(string key, T defaultValue)
    {
        return GetValue(key, defaultValue, null!);
    }

    public T GetValue<T>(string key, T defaultValue, Action<T> onUpdate)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        var pluginName = GetPluginName();
        
        // If plugin name not available yet, try to find it by searching config sections
        // This is needed for plugins that read config during construction, before SetName is called
        if (string.IsNullOrEmpty(pluginName))
        {
            pluginName = FindPluginNameByKey(key);
            if (string.IsNullOrEmpty(pluginName))
            {
                _logger.LogDebug("Plugin name not yet available for configuration wrapper. Cannot retrieve key: {Key}. Returning default.", key);
                return defaultValue;
            }
        }

        if (!TryGetValueFromConfig(pluginName, key, out T? value))
        {
            return defaultValue;
        }

        if (onUpdate != null)
        {
            RegisterUpdateCallback(key, onUpdate);
        }

        return value;
    }

    private bool TryGetValueFromConfig<T>(string pluginName, string key, out T value)
    {
        value = default!;

        var config = GetOrLoadConfig();
        if (!config.TryGetValue(pluginName, out var pluginConfig))
        {
            return false;
        }

        if (!TryFindKeyCaseInsensitive(pluginConfig, key, out var actualKey))
        {
            return false;
        }

        var item = pluginConfig[actualKey];
        if (item == null)
        {
            return false;
        }

        try
        {
            value = ConvertValue<T>(item);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert configuration value for key {Key} to type {Type}", key, typeof(T).Name);
            return false;
        }
    }

    private void RegisterUpdateCallback<T>(string key, Action<T> callback)
    {
        lock (_updateCallbacks)
        {
            if (!_updateCallbacks.TryGetValue(key, out var callbacks))
            {
                callbacks = new List<Action<object>>();
                _updateCallbacks[key] = callbacks;
            }

            callbacks.Add(obj =>
            {
                try
                {
                    var converted = ConvertValue<T>(obj);
                    callback(converted);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to convert value for update callback key {Key} to type {Type}", key, typeof(T).Name);
                }
            });
        }
    }

    public async Task SetValueAsync(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (string.IsNullOrEmpty(_pluginName))
        {
            // Queue the write to be applied once the plugin name is set
            lock (_pendingWrites)
            {
                _pendingWrites.Add((key, value));
            }

            _logger.LogDebug("Plugin name not set for configuration wrapper. Queued write for key: {Key}", key);
            return;
        }

        var configToUse = GetOrLoadConfig();

        // Ensure plugin section exists
        if (!configToUse.TryGetValue(_pluginName, out var pluginConfig))
        {
            pluginConfig = new Dictionary<string, object>();
            configToUse[_pluginName] = pluginConfig;
        }

        // Convert value if needed (handle numeric types from JSON)
        var castValue = ConvertValueForStorage(value);

        // Case-insensitive key lookup - find existing key with different case, or add new one
        if (TryFindKeyCaseInsensitive(pluginConfig, key, out var existingKey))
        {
            // Update existing key (preserving original case)
            pluginConfig[existingKey] = castValue;
        }
        else
        {
            // Add new key
            pluginConfig[key] = castValue;
        }

        await _configHandler.Set(configToUse);

        // Update our cached reference
        lock (_configLock)
        {
            _config = configToUse;
        }
    }

    private void OnConfigurationUpdated(ScriptPluginConfiguration updatedConfig)
    {
        if (string.IsNullOrEmpty(_pluginName) || !updatedConfig.TryGetValue(_pluginName, out var pluginConfig))
        {
            return;
        }

        lock (_updateCallbacks)
        {
            foreach (var (key, callbacks) in _updateCallbacks.ToList())
            {
                if (!TryFindKeyCaseInsensitive(pluginConfig, key, out var actualKey))
                {
                    continue;
                }

                var value = pluginConfig[actualKey];
                if (value == null)
                {
                    continue;
                }

                foreach (var callback in callbacks.ToList())
                {
                    try
                    {
                        callback(value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error invoking update callback for key {Key}", key);
                    }
                }
            }
        }
    }

    private T ConvertValue<T>(object value)
    {
        if (value == null)
        {
            return default!;
        }

        // Handle JsonElement from JSON deserialization
        if (value is JsonElement jsonElement)
        {
            return ConvertJsonElement<T>(jsonElement);
        }

        // If already the correct type, return as-is
        if (value is T directValue)
        {
            return directValue;
        }

        // Try standard conversion
        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception ex)
        {
            // If conversion fails, try JSON deserialization for string values
            if (value is string str)
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(str)!;
                }
                catch (Exception deserializeEx)
                {
                    _logger.LogDebug(deserializeEx, "Failed to deserialize string value to type {Type}", typeof(T).Name);
                    // Fall through to return default
                }
            }
            else
            {
                _logger.LogDebug(ex, "Failed to convert value of type {SourceType} to type {TargetType}", value.GetType().Name, typeof(T).Name);
            }

            return default!;
        }
    }

    private static T ConvertJsonElement<T>(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => (T)(object)element.GetString()!,
            JsonValueKind.Number => ConvertJsonNumber<T>(element),
            JsonValueKind.True or JsonValueKind.False => (T)(object)element.GetBoolean(),
            JsonValueKind.Array => ConvertJsonArray<T>(element),
            JsonValueKind.Object => JsonSerializer.Deserialize<T>(element.GetRawText())!,
            _ => default!
        };
    }

    private static T ConvertJsonNumber<T>(JsonElement element)
    {
        var targetType = typeof(T);
        
        return targetType switch
        {
            _ when targetType == typeof(int) => (T)(object)element.GetInt32(),
            _ when targetType == typeof(long) => (T)(object)element.GetInt64(),
            _ when targetType == typeof(double) => (T)(object)element.GetDouble(),
            _ when targetType == typeof(float) => (T)(object)element.GetSingle(),
            _ when targetType == typeof(decimal) => (T)(object)element.GetDecimal(),
            _ when targetType == typeof(bool) => (T)(object)(element.GetInt32() != 0),
            _ => (T)(object)element.GetInt32() // Default to int for unknown numeric types
        };
    }

    private static T ConvertJsonArray<T>(JsonElement element)
    {
        var list = new List<object>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(item.GetRawText());
        }

        return (T)(object)list;
    }

    private static object ConvertValueForStorage(object value)
    {
        // Convert double to int if it's a whole number (for JSON compatibility)
        if (value is double doubleValue)
        {
            var intValue = (int)doubleValue;
            return Math.Abs(doubleValue - intValue) < double.Epsilon ? intValue : value;
        }

        // Convert double arrays to int arrays if all values are whole numbers
        if (value is double[] doubleArray && doubleArray.All(d => Math.Abs(d - (int)d) < double.Epsilon))
        {
            return doubleArray.Select(d => (int)d).ToArray();
        }

        return value;
    }
}
