using Microsoft.Extensions.FileProviders;

namespace SharedLibraryCore.Interfaces;

/// <summary>
/// In-memory store for plugin-supplied web assets (css/js/images) served under /_content/{pluginId}/.
/// Assets are never written to disk; the backing store is mutated as plugins load (including remote
/// plugins that register after the web host has started), so <see cref="FileProvider"/> must be a
/// stable reference that performs live lookups.
/// </summary>
public interface IPluginAssetStore
{
    /// <summary>
    /// Stable provider bound once by the static-file middleware; reads the live backing store.
    /// </summary>
    IFileProvider FileProvider { get; }

    /// <summary>
    /// Registers (or overwrites) a plugin's web assets. Keys are relative paths within the plugin's
    /// web root (e.g. "plugin.css", "images/logo.png").
    /// </summary>
    void RegisterPlugin(string pluginId, IReadOnlyDictionary<string, byte[]> assets);

    /// <summary>
    /// Looks up an asset by its request sub-path (relative to /_content), e.g. "/credify/plugin.css".
    /// </summary>
    bool TryGet(string requestPath, out byte[] content);
}
