using Microsoft.Extensions.FileProviders;

namespace SharedLibraryCore.Plugins;

/// <summary>
/// <see cref="IFileInfo"/> over an in-memory asset buffer. <see cref="Name"/> keeps the real file
/// extension so the static-file middleware can resolve the correct content type.
/// </summary>
internal sealed class InMemoryFileInfo(string name, byte[] content, DateTimeOffset lastModified) : IFileInfo
{
    public bool Exists => true;
    public long Length => content.Length;
    public string? PhysicalPath => null;
    public string Name => name;
    // The asset's registration time, NOT the Unix epoch: with no Cache-Control a browser caches for
    // ~10% of (now - Last-Modified), and an epoch date made that ~5 years. It also seeds the ETag.
    public DateTimeOffset LastModified => lastModified;
    public bool IsDirectory => false;

    // Wraps the shared buffer without copying. The static-file middleware owns the returned stream and
    // disposes it after writing the response; a MemoryStream over an existing byte[] holds no unmanaged
    // resources, so disposal is benign and the underlying asset buffer is left intact for reuse.
    public Stream CreateReadStream() => new MemoryStream(content, writable: false);
}
