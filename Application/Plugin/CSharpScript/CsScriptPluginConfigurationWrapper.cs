using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using IW4MAdmin.Application.Configuration;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Interfaces;
using System.IO;

namespace IW4MAdmin.Application.Plugin.CSharpScript;

/// <summary>
/// Implementation of ICsScriptPluginConfiguration that wraps ScriptPluginConfiguration.
/// Provides simple key-value access to plugin settings stored in ScriptPluginSettings.json.
/// </summary>
internal class CsScriptPluginConfigurationWrapper : ICsScriptPluginConfiguration
{
    private ScriptPluginConfiguration _config;
    private readonly IConfigurationHandlerV2<ScriptPluginConfiguration> _configHandler;
    private readonly ILogger<CsScriptPluginConfigurationWrapper> _logger;
    private readonly List<(string key, Action<object> callback)> _updateCallbacks = new();
    private readonly List<(string key, object value)> _pendingWrites = new();
    private readonly List<(string key, object defaultValue, object onUpdate)> _pendingReads = new();
    private readonly Func<string?>? _pluginNameProvider;
    private readonly object _configLock = new();
    private string _pluginName = string.Empty;

    public CsScriptPluginConfigurationWrapper(
        IConfigurationHandlerV2<ScriptPluginConfiguration> configHandler,
        ILogger<CsScriptPluginConfigurationWrapper> logger,
        Func<string?>? pluginNameProvider = null)
    {
        _configHandler = configHandler;
        _logger = logger;
        _pluginNameProvider = pluginNameProvider;
        _config = configHandler.Get("ScriptPluginSettings", new ScriptPluginConfiguration()).GetAwaiter().GetResult();
        _configHandler.Updated += OnConfigurationUpdated;
    }

    public void SetName(string name)
    {
        _pluginName = name;
        
        // Apply any pending writes that occurred before the name was set
        // Only write if the key doesn't already exist (don't overwrite user settings)
        if (_pendingWrites.Count > 0)
        {
            lock (_pendingWrites)
            {
                foreach (var (key, value) in _pendingWrites)
                {
                    // Refresh config before checking (might have been updated)
                    ScriptPluginConfiguration currentConfig;
                    try
                    {
                        var latestConfig = _configHandler.Get("ScriptPluginSettings", new ScriptPluginConfiguration()).GetAwaiter().GetResult();
                        lock (_configLock)
                        {
                            _config = latestConfig;
                            currentConfig = _config;
                        }
                    }
                    catch
                    {
                        lock (_configLock)
                        {
                            currentConfig = _config;
                        }
                    }

                    // Check if key exists before writing (case-insensitive)
                    if (currentConfig.TryGetValue(_pluginName, out var pluginConfig))
                    {
                        var existingKey = pluginConfig.Keys.FirstOrDefault(k => 
                            string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
                        
                        // Only write if key doesn't exist
                        if (existingKey == null)
                        {
                            _ = SetValueAsync(key, value);
                        }
                    }
                    else
                    {
                        // Plugin section doesn't exist, safe to write
                        _ = SetValueAsync(key, value);
                    }
                }
                _pendingWrites.Clear();
            }
        }
        
        // Note: Pending reads can't be re-executed because GetValue is synchronous
        // Plugins will need to re-read values after SetName is called, or we'd need to make GetValue async
        // For now, we'll log a warning that values read before name was set used defaults
        if (_pendingReads.Count > 0)
        {
            _logger.LogWarning("Plugin name was not available during initial config reads. {Count} value(s) were read with defaults. Values should be re-read after plugin initialization.", _pendingReads.Count);
            lock (_pendingReads)
            {
                _pendingReads.Clear();
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
        // Try to get plugin name from stored value or provider
        var pluginName = _pluginName;
        if (string.IsNullOrEmpty(pluginName) && _pluginNameProvider != null)
        {
            pluginName = _pluginNameProvider();
            if (!string.IsNullOrEmpty(pluginName))
            {
                _pluginName = pluginName; // Cache it for future calls
            }
        }
        
        if (string.IsNullOrEmpty(pluginName))
        {
            // Plugin name not available yet - try to find matching config section by scanning all sections
            // This is a fallback for when the plugin name isn't set yet during construction
            // We'll try to match by searching for a section that has the requested key
            lock (_configLock)
            {
                ScriptPluginConfiguration currentConfig;
                try
                {
                    currentConfig = _configHandler.Get("ScriptPluginSettings", new ScriptPluginConfiguration()).GetAwaiter().GetResult();
                }
                catch
                {
                    currentConfig = _config;
                }

                // Search through all plugin sections to find one with this key
                foreach (var section in currentConfig)
                {
                    var sectionConfig = section.Value;
                    if (sectionConfig != null)
                    {
                        // Case-insensitive key lookup
                        var foundKey = sectionConfig.Keys.FirstOrDefault(k => 
                            string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
                        
                        if (foundKey != null)
                        {
                            // Found a section with this key - use this section as the plugin name
                            pluginName = section.Key;
                            _pluginName = pluginName; // Cache it
                            break;
                        }
                    }
                }
            }
            
            if (string.IsNullOrEmpty(pluginName))
            {
                _logger.LogDebug("Plugin name not yet available for configuration wrapper. Cannot retrieve key: {Key}. Returning default.", key);
                return defaultValue;
            }
        }

        // Refresh config from handler in case it was updated (handles file reload scenarios)
        // This ensures we're reading the latest config state even if file watcher events were missed
        ScriptPluginConfiguration configToUse;
        try
        {
            var latestConfig = _configHandler.Get("ScriptPluginSettings", new ScriptPluginConfiguration()).GetAwaiter().GetResult();
            lock (_configLock)
            {
                // Update our cached config reference with latest values
                _config = latestConfig;
                configToUse = _config;
            }
        }
        catch (Exception ex)
        {
            // If refresh fails (e.g., file locked), use cached config
            lock (_configLock)
            {
                configToUse = _config;
            }
        }

        if (!configToUse.TryGetValue(pluginName, out var pluginConfig))
        {
            return defaultValue;
        }

        // Case-insensitive key lookup (JSON keys may vary in case)
        var item = pluginConfig.FirstOrDefault(kvp => 
            string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
        
        if (item == null)
        {
            return defaultValue;
        }

        // Register update callback if provided
        if (onUpdate != null)
        {
            lock (_updateCallbacks)
            {
                _updateCallbacks.Add((key, (obj) => onUpdate((T)obj)));
            }
        }

        try
        {
            var converted = ConvertValue<T>(item);
            return converted;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert configuration value for key {Key} to type {Type}", key, typeof(T).Name);
            return defaultValue;
        }
    }

    public async Task SetValueAsync(string key, object value)
    {
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

        // Refresh config before writing to ensure we have latest state
        ScriptPluginConfiguration configToUse;
        try
        {
            var latestConfig = _configHandler.Get("ScriptPluginSettings", new ScriptPluginConfiguration()).GetAwaiter().GetResult();
            lock (_configLock)
            {
                _config = latestConfig;
                configToUse = _config;
            }
        }
        catch
        {
            lock (_configLock)
            {
                configToUse = _config;
            }
        }

        // Ensure plugin section exists
        if (!configToUse.TryGetValue(_pluginName, out var pluginConfig))
        {
            pluginConfig = new Dictionary<string, object>();
            configToUse.Add(_pluginName, pluginConfig);
        }

        // Convert value if needed (handle numeric types from JSON)
        var castValue = ConvertValueForStorage(value);

        // Case-insensitive key lookup - find existing key with different case
        var existingKey = pluginConfig.Keys.FirstOrDefault(k => 
            string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        
        if (existingKey != null)
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
        lock (_updateCallbacks)
        {
            foreach (var (key, callback) in _updateCallbacks.ToList())
            {
                try
                {
                    if (!updatedConfig.TryGetValue(_pluginName, out var value1)) continue;
                    
                    // Case-insensitive key lookup
                    var value = value1.FirstOrDefault(kvp => 
                        string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
                    
                    if (value == null) continue;
                    
                    var convertedValue = ConvertValue<object>(value);
                    callback(convertedValue);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error invoking update callback for key {Key}", key);
                }
            }
        }
    }

    private static T ConvertValue<T>(object value)
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
        catch
        {
            // If conversion fails, try JSON deserialization
            if (value is string str)
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(str)!;
                }
                catch
                {
                    // Fall through to return default
                }
            }

            return default!;
        }
    }

    private static T ConvertJsonElement<T>(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return (T)(object)element.GetString()!;
            case JsonValueKind.Number:
                if (typeof(T) == typeof(int))
                    return (T)(object)element.GetInt32();
                if (typeof(T) == typeof(long))
                    return (T)(object)element.GetInt64();
                if (typeof(T) == typeof(double))
                    return (T)(object)element.GetDouble();
                if (typeof(T) == typeof(float))
                    return (T)(object)element.GetSingle();
                if (typeof(T) == typeof(decimal))
                    return (T)(object)element.GetDecimal();
                if (typeof(T) == typeof(bool))
                    return (T)(object)(element.GetInt32() != 0);
                return (T)(object)element.GetInt32(); // Default to int
            case JsonValueKind.True:
            case JsonValueKind.False:
                return (T)(object)element.GetBoolean();
            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(item.GetRawText());
                }

                return (T)(object)list;
            case JsonValueKind.Object:
                return JsonSerializer.Deserialize<T>(element.GetRawText())!;
            default:
                return default!;
        }
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
