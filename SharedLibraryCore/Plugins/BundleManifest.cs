namespace SharedLibraryCore.Plugins;

/// <summary>
/// Describes the contents of a plugin bundle (manifest.json at the bundle root).
/// Deserialized case-insensitively, so camelCase JSON maps onto these properties.
/// </summary>
public class BundleManifest
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Stable identifier; also the /_content/{id} namespace and the loader cache key.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;

    /// <summary>Advisory: the SharedLibraryCore/API version the plugin was built against.</summary>
    public string? TargetApiVersion { get; set; }

    /// <summary>Path within the bundle to the plugin assembly, e.g. "lib/Credify.dll".</summary>
    public string EntryAssembly { get; set; } = string.Empty;

    /// <summary>Folder within the bundle whose contents are served at /_content/{id}/. Defaults to "wwwroot".</summary>
    public string WebRoot { get; set; } = "wwwroot";

    /// <summary>Folder within the bundle holding game-side scripts, extracted to the plugin's sandbox
    /// (<c>Plugins/&lt;id&gt;/gsc/</c>) on load for the instance owner to copy to their game server.
    /// Defaults to "gsc".</summary>
    public string GscRoot { get; set; } = "gsc";

    /// <summary>Folder within the bundle whose contents are extracted to the plugin's own data folder
    /// (<c>Plugins/&lt;id&gt;/Resources/</c>) on load. Lets a plugin ship arbitrary data files (databases,
    /// assets, …) and read them from disk at runtime without any operator setup. Defaults to "resources".</summary>
    public string ResourceRoot { get; set; } = "resources";
}
