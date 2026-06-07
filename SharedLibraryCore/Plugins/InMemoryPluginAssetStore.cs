using System.Collections.Concurrent;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Plugins;

/// <summary>
/// <see cref="IPluginAssetStore"/> backed by a thread-safe in-memory map. Plugin web assets are
/// keyed by their request sub-path under /_content (e.g. "/credify/plugin.css") and are never
/// persisted to disk. The exposed <see cref="FileProvider"/> performs live lookups so assets
/// registered after the static-file middleware was built (remote plugins) are still served.
/// </summary>
public sealed class InMemoryPluginAssetStore : IPluginAssetStore
{
    /// <summary>
    /// Process-wide instance. Exposed as a static so the WebfrontCore startup paths (which run before
    /// the DI container is built) and the DI-resolved consumers share a single backing store.
    /// </summary>
    public static InMemoryPluginAssetStore Shared { get; } = new();

    private readonly ConcurrentDictionary<string, byte[]> _assets =
        new(StringComparer.OrdinalIgnoreCase);

    public InMemoryPluginAssetStore()
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
            _assets[Normalize($"{pluginId}/{relativePath}")] = content;
        }
    }

    public bool TryGet(string requestPath, out byte[] content) =>
        _assets.TryGetValue(Normalize(requestPath), out content!);

    private static string Normalize(string path) =>
        "/" + path.Replace('\\', '/').TrimStart('/');

    /// <summary>
    /// <see cref="IFileProvider"/> that resolves files from the owning store's live map.
    /// </summary>
    private sealed class InMemoryPluginFileProvider(InMemoryPluginAssetStore store) : IFileProvider
    {
        public IFileInfo GetFileInfo(string subpath)
        {
            if (store.TryGet(subpath, out var content))
            {
                var name = subpath.Replace('\\', '/').TrimEnd('/');
                var slash = name.LastIndexOf('/');
                if (slash >= 0)
                {
                    name = name[(slash + 1)..];
                }

                return new InMemoryFileInfo(name, content);
            }

            return new NotFoundFileInfo(subpath);
        }

        public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }

    private sealed class InMemoryFileInfo(string name, byte[] content) : IFileInfo
    {
        public bool Exists => true;
        public long Length => content.Length;
        public string? PhysicalPath => null;
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public bool IsDirectory => false;

        public Stream CreateReadStream() => new MemoryStream(content, writable: false);
    }
}
