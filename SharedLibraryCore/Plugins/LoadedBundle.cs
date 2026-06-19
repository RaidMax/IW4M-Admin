using System.Reflection;

namespace SharedLibraryCore.Plugins;

/// <summary>
/// Result of loading a plugin bundle: the loaded assembly plus its parsed manifest and resources.
/// Web assets are held in memory (served via the asset store); GSC files and resource files are surfaced
/// for extraction to the plugin's own sandbox folder.
/// </summary>
public sealed class LoadedBundle
{
    /// <summary>The bundle's stable identifier — a convenience projection of <see cref="Manifest"/>.Id.</summary>
    public string Id => Manifest.Id;

    public required Assembly Assembly { get; init; }
    public required BundleManifest Manifest { get; init; }
    public required IReadOnlyDictionary<string, byte[]> WebAssets { get; init; }

    /// <summary>Game-side scripts, keyed by path relative to the gsc root. Extracted to the plugin's sandbox
    /// (<c>Plugins/&lt;id&gt;/gsc/</c>) on load; the instance owner copies them to their game server.</summary>
    public required IReadOnlyDictionary<string, byte[]> GscFiles { get; init; }

    /// <summary>Generic bundle data files, keyed by path relative to the bundle's resource root. Extracted to
    /// the plugin's sandbox folder (<c>Plugins/&lt;id&gt;/Resources/</c>) by the host on load.</summary>
    public required IReadOnlyDictionary<string, byte[]> ResourceFiles { get; init; }
}
