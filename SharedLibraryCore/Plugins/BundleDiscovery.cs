namespace SharedLibraryCore.Plugins;

/// <summary>Read-only discovery of plugin bundles in the Plugins folder. Performs no extraction.</summary>
public static class BundleDiscovery
{
    public readonly record struct BundleSource(string Path, bool IsZip);

    /// <summary>
    /// Enumerates bundle sources directly under <paramref name="pluginDir"/>: every *.zip file, and
    /// every immediate subdirectory containing a manifest.json at its root (pre-unpacked dev folder).
    /// </summary>
    public static IEnumerable<BundleSource> Enumerate(string pluginDir)
    {
        if (!Directory.Exists(pluginDir))
        {
            yield break;
        }

        foreach (var zip in Directory.EnumerateFiles(pluginDir, "*.zip", SearchOption.TopDirectoryOnly))
        {
            yield return new BundleSource(zip, IsZip: true);
        }

        foreach (var dir in Directory.EnumerateDirectories(pluginDir))
        {
            if (File.Exists(Path.Combine(dir, "manifest.json")))
            {
                yield return new BundleSource(dir, IsZip: false);
            }
        }
    }
}
