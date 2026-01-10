namespace SharedLibraryCore.Interfaces;

/// <summary>
/// Provides access to the shared ScriptPluginSettings.json configuration file for C# script plugins.
/// This allows plugins to use the same simple key-value configuration system as JavaScript plugins.
/// </summary>
public interface ICsScriptPluginConfiguration
{
    /// <summary>
    /// Gets a configuration value by key. Returns default(T) if the key doesn't exist.
    /// </summary>
    T GetValue<T>(string key);

    /// <summary>
    /// Gets a configuration value by key, or returns the default value if the key doesn't exist.
    /// </summary>
    T GetValue<T>(string key, T defaultValue);

    /// <summary>
    /// Gets a configuration value by key and registers a callback to be invoked when the value changes (hot-reload).
    /// </summary>
    T GetValue<T>(string key, T defaultValue, Action<T> onUpdate);

    /// <summary>
    /// Sets a configuration value by key. The change is persisted to ScriptPluginSettings.json.
    /// </summary>
    Task SetValueAsync(string key, object value);

    /// <summary>
    /// Sets the plugin name used as the key in ScriptPluginSettings.json.
    /// This is called automatically by the plugin loading system.
    /// </summary>
    void SetName(string name);
}
