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

    /// <summary>Folder within the bundle holding game-side scripts extracted to disk. Defaults to "gsc".</summary>
    public string GscRoot { get; set; } = "gsc";
}
