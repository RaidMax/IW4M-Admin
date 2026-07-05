using System.Collections.Concurrent;
using Microsoft.Extensions.FileProviders;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Plugins;

/// <summary>
/// <see cref="IPluginAssetStorage"/> backed by a thread-safe in-memory map. Plugin web assets are
/// keyed by their request sub-path under /_content (e.g. "/credify/plugin.css") and are never
/// persisted to disk. The exposed <see cref="FileProvider"/> performs live lookups so assets
/// registered after the static-file middleware was built (remote plugins) are still served.
/// </summary>
public sealed class InMemoryPluginAssetStorage : IPluginAssetStorage
{
    /// <summary>
    /// Process-wide instance. Exposed as a static so the WebfrontCore startup paths (which run before
    /// the DI container is built) and the DI-resolved consumers share a single backing store.
    /// </summary>
    public static InMemoryPluginAssetStorage Shared { get; } = new();

    private readonly ConcurrentDictionary<string, byte[]> _assets =
        new(StringComparer.OrdinalIgnoreCase);

    public InMemoryPluginAssetStorage()
    {
        FileProvider = new InMemoryPluginFileProvider(this);
    }

    public IFileProvider FileProvider { get; }

    public void RegisterPlugin(string pluginId, IReadOnlyDictionary<string, byte[]> assets)
    {
        if (string.IsNullOrWhiteSpace(pluginId) || assets is null)
        {
            return;
        }

        foreach (var (relativePath, content) in assets)
        {
            _assets[$"{pluginId}/{relativePath}".ToNormalizedBundlePath()] = content;
        }
    }

    public bool TryGet(string requestPath, out byte[] content) =>
        _assets.TryGetValue(requestPath.ToNormalizedBundlePath(), out content!);
}
