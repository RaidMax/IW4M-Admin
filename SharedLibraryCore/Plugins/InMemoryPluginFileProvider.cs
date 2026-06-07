using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace SharedLibraryCore.Plugins;

/// <summary>
/// <see cref="IFileProvider"/> that resolves files from the owning <see cref="InMemoryPluginAssetStorage"/>'s
/// live map. Bound once by the static-file middleware but reads the store on every request, so assets
/// registered after startup (remote plugins) are still served.
/// </summary>
internal sealed class InMemoryPluginFileProvider(InMemoryPluginAssetStorage store) : IFileProvider
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

    // Part of the IFileProvider contract — the static-file middleware can subscribe to change tokens to
    // invalidate cached file info. In-memory plugin assets don't raise change notifications (a plugin's
    // assets are replaced wholesale on reload, not edited in place), so a no-op token is returned.
    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
}
