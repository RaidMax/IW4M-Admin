namespace SharedLibraryCore.Interfaces;

/// <summary>
/// A plugin's own isolated data directory on disk — <c>Plugins/&lt;plugin&gt;/</c>, next to the
/// plugin binary. Inject <c>IPluginDataDirectory&lt;YourPlugin&gt;</c> to read and write files your
/// plugin owns (caches, exports, seeded assets — anything that is not a routed config or database).
///
/// The folder name is derived from the plugin's own assembly and the directory is created on first
/// access; callers only ever use relative paths inside it. This mirrors PaperMC's
/// <c>getDataFolder()</c>: the host owns "where", the plugin owns "what".
/// </summary>
/// <typeparam name="TPlugin">
/// The plugin's own type (any type defined in the plugin assembly). Its assembly identifies the
/// folder, so the same instance is shared by everything in that plugin.
/// </typeparam>
public interface IPluginDataDirectory<TPlugin>
{
    /// <summary>
    /// Absolute path to the plugin's data folder. Created on first access.
    /// </summary>
    string RootPath { get; }

    /// <summary>
    /// Resolves a path inside the plugin folder, creating any intermediate directories along the way
    /// (e.g. <c>GetPath("playerdata", "42", "stats.json")</c>). Throws if the resulting path would
    /// escape the plugin folder.
    /// </summary>
    string GetPath(params string[] segments);

    /// <summary>
    /// Copies a resource embedded in the plugin assembly out into the plugin folder on first run
    /// (handy for seeding a default file). No-op when the target already exists unless
    /// <paramref name="overwrite"/> is set. Nested resource paths are supported. Returns the written
    /// path.
    /// </summary>
    Task<string> EnsureAsset(string embeddedResourceName, bool overwrite = false);
}
