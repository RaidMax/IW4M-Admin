using System.Reflection;

namespace SharedLibraryCore.Plugins;

/// <summary>
/// Result of loading a plugin bundle: the loaded assembly plus its parsed manifest and resources.
/// Web assets are held in memory (served via the asset store); GSC files are surfaced for optional
/// extraction to the configured game-files folder.
/// </summary>
public sealed class LoadedBundle
{
    /// <summary>The bundle's stable identifier — a convenience projection of <see cref="Manifest"/>.Id.</summary>
    public string Id => Manifest.Id;

    public required Assembly Assembly { get; init; }
    public required BundleManifest Manifest { get; init; }
    public required IReadOnlyDictionary<string, byte[]> WebAssets { get; init; }
    public required IReadOnlyDictionary<string, byte[]> GscFiles { get; init; }
}
