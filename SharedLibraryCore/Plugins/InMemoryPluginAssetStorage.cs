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

    private readonly ConcurrentDictionary<string, StoredAsset> _assets =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// An asset plus the moment it was registered. The timestamp is served as Last-Modified and feeds
    /// the static-file middleware's ETag, so a redeployed plugin (whose assets are re-registered on
    /// load) invalidates browser caches even when its manifest version wasn't bumped.
    /// </summary>
    internal readonly record struct StoredAsset(byte[] Content, DateTimeOffset RegisteredAt);

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

        // HTTP dates have one-second resolution; truncate so Last-Modified round-trips exactly through
        // If-Modified-Since.
        var now = DateTimeOffset.UtcNow;
        var registeredAt = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));

        foreach (var (relativePath, content) in assets)
        {
            _assets[$"{pluginId}/{relativePath}".ToNormalizedBundlePath()] = new StoredAsset(content, registeredAt);
        }
    }

    public bool TryGet(string requestPath, out byte[] content)
    {
        if (_assets.TryGetValue(requestPath.ToNormalizedBundlePath(), out var asset))
        {
            content = asset.Content;
            return true;
        }

        content = null!;
        return false;
    }

    internal bool TryGetAsset(string requestPath, out StoredAsset asset) =>
        _assets.TryGetValue(requestPath.ToNormalizedBundlePath(), out asset);
}
