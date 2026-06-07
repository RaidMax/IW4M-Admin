using SharedLibraryCore.Plugins;

namespace SharedLibraryCore.Interfaces;

/// <summary>
/// Loads plugin bundles (DLL + web assets + optional GSC) in memory. Implementations cache by
/// manifest id and return the same <see cref="LoadedBundle"/> on repeat calls, so the two host
/// load sites (Razor/MVC discovery and plugin lifecycle) never load the same assembly twice
/// (Assembly.Load(byte[]) produces a new identity on every call).
/// </summary>
public interface IPluginBundleLoader
{
    /// <summary>Loads a bundle from raw zip bytes (local zip or decrypted remote payload).</summary>
    LoadedBundle? LoadFromZipBytes(byte[] zipBytes, string sourceLabel);

    /// <summary>Loads a bundle from a pre-unpacked directory (dev convenience).</summary>
    LoadedBundle? LoadFromDirectory(string directory);

    /// <summary>All bundles loaded so far.</summary>
    IReadOnlyCollection<LoadedBundle> LoadedBundles { get; }
}
