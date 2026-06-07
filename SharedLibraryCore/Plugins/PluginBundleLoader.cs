using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Plugins;

/// <summary>
/// <see cref="IPluginBundleLoader"/> that opens bundles in memory and caches by manifest id.
/// Web assets are pushed into <see cref="InMemoryPluginAssetStorage.Shared"/>. The plugin assembly is loaded
/// into the <b>default</b> <see cref="AssemblyLoadContext"/> from memory (never written to disk) — the same
/// context loose plugin dlls use — so Blazor routing, the host layout and interactive rendering treat plugin
/// pages exactly like first-party pages (a custom/isolated load context renders pages without the host's
/// layout). Private dependencies shipped in <c>lib/</c> are resolved on demand, also in-memory, via the
/// default context's <see cref="AssemblyLoadContext.Resolving"/> hook. Only GSC files are surfaced for on-disk
/// extraction by the caller.
/// </summary>
public sealed class PluginBundleLoader(IPluginAssetStorage assetStore) : IPluginBundleLoader
{
    /// <summary>
    /// Process-wide instance. Static so the WebfrontCore startup paths (which run before the DI container
    /// is built) and DI-resolved consumers share one cache and one set of assembly identities.
    /// </summary>
    public static PluginBundleLoader Shared { get; } = new(InMemoryPluginAssetStorage.Shared);

    private const string ManifestFileName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // private dependencies shipped inside bundles, resolved on demand into the default context (in-memory)
    private static readonly ConcurrentDictionary<string, byte[]> DependencyBytes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Assembly> ResolvedDependencies = new(StringComparer.OrdinalIgnoreCase);

    static PluginBundleLoader()
    {
        AssemblyLoadContext.Default.Resolving += ResolveBundleDependency;
    }

    private static Assembly? ResolveBundleDependency(AssemblyLoadContext context, AssemblyName name)
    {
        if (name.Name is null)
        {
            return null;
        }

        if (ResolvedDependencies.TryGetValue(name.Name, out var existing))
        {
            return existing;
        }

        if (!DependencyBytes.TryGetValue(name.Name, out var bytes))
        {
            return null;
        }

        using var stream = new MemoryStream(bytes, writable: false);
        var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
        ResolvedDependencies[name.Name] = assembly;
        return assembly;
    }

    private readonly Lock _loadLock = new();
    private readonly Dictionary<string, LoadedBundle> _cache = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<LoadedBundle> LoadedBundles
    {
        get
        {
            lock (_loadLock)
            {
                return _cache.Values.ToArray();
            }
        }
    }

    public LoadedBundle? LoadFromZipBytes(byte[] zipBytes, string sourceLabel)
    {
        try
        {
            using var memoryStream = new MemoryStream(zipBytes, writable: false);
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

            var manifest = ReadManifest(ReadEntryBytes(archive, ManifestFileName), sourceLabel);

            lock (_loadLock)
            {
                if (_cache.TryGetValue(manifest.Id, out var cached))
                {
                    return cached;
                }

                // read sequentially: ZipArchive is not thread-safe, so the entry reads can't share the
                // archive across threads (the gain would be marginal for a handful of small entries anyway).
                var libAssemblies = ReadFolder(archive, LibFolder(manifest));
                var webAssets = ReadFolder(archive, manifest.WebRoot);
                var gscFiles = ReadFolder(archive, manifest.GscRoot);

                var assembly = LoadEntryInMemory(manifest, libAssemblies);
                return RegisterBundle(manifest, assembly, webAssets, gscFiles);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load plugin bundle '{sourceLabel}': {ex.Message}", ex);
        }
    }

    public LoadedBundle? LoadFromDirectory(string directory)
    {
        try
        {
            var manifestPath = Path.Combine(directory, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                throw new InvalidDataException($"Directory is missing {ManifestFileName}");
            }

            var manifest = ReadManifest(File.ReadAllBytes(manifestPath), directory);

            lock (_loadLock)
            {
                if (_cache.TryGetValue(manifest.Id, out var cached))
                {
                    return cached;
                }

                // read sequentially — these are a handful of small files read once at startup, so the
                // overhead of parallelizing the disk reads would outweigh any benefit.
                var libAssemblies = ReadDirectory(directory, LibFolder(manifest));
                var webAssets = ReadDirectory(directory, manifest.WebRoot);
                var gscFiles = ReadDirectory(directory, manifest.GscRoot);

                // load in-memory (same path as the zip/premium channel) so local and remote behave identically
                var assembly = LoadEntryInMemory(manifest, libAssemblies);
                return RegisterBundle(manifest, assembly, webAssets, gscFiles);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load plugin bundle directory '{directory}': {ex.Message}", ex);
        }
    }

    // loads the entry assembly (and exposes its private deps) entirely in memory, for the encrypted/zip path
    private static Assembly LoadEntryInMemory(BundleManifest manifest, IReadOnlyDictionary<string, byte[]> libAssemblies)
    {
        var entryName = Path.GetFileNameWithoutExtension(manifest.EntryAssembly.ToNormalizedBundlePath());

        var byName = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (relativePath, bytes) in libAssemblies)
        {
            if (relativePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                byName[Path.GetFileNameWithoutExtension(relativePath)] = bytes;
            }
        }

        if (!byName.TryGetValue(entryName, out var entryBytes))
        {
            throw new InvalidDataException($"entry assembly '{manifest.EntryAssembly}' not found in bundle");
        }

        foreach (var (simpleName, bytes) in byName)
        {
            if (!string.Equals(simpleName, entryName, StringComparison.OrdinalIgnoreCase))
            {
                DependencyBytes.TryAdd(simpleName, bytes);
            }
        }

        using var entryStream = new MemoryStream(entryBytes, writable: false);
        var assembly = AssemblyLoadContext.Default.LoadFromStream(entryStream);

        var assemblyName = assembly.GetName().Name;
        if (assemblyName is not null)
        {
            ResolvedDependencies[assemblyName] = assembly;
        }

        return assembly;
    }

    private LoadedBundle RegisterBundle(BundleManifest manifest, Assembly assembly,
        IReadOnlyDictionary<string, byte[]> webAssets, IReadOnlyDictionary<string, byte[]> gscFiles)
    {
        assetStore.RegisterPlugin(manifest.Id, webAssets);

        var bundle = new LoadedBundle
        {
            Assembly = assembly,
            Manifest = manifest,
            WebAssets = webAssets,
            GscFiles = gscFiles
        };

        _cache[manifest.Id] = bundle;
        return bundle;
    }

    private static string LibFolder(BundleManifest manifest)
    {
        var entry = manifest.EntryAssembly.ToNormalizedBundlePath();
        var slash = entry.IndexOf('/');
        return slash > 0 ? entry[..slash] : "lib";
    }

    private static BundleManifest ReadManifest(byte[]? manifestBytes, string sourceLabel)
    {
        if (manifestBytes is null)
        {
            throw new InvalidDataException("bundle is missing manifest.json");
        }

        BundleManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<BundleManifest>(manifestBytes, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"bundle '{sourceLabel}' has an invalid {ManifestFileName}: {ex.Message}", ex);
        }

        if (manifest is null)
        {
            throw new InvalidDataException($"bundle '{sourceLabel}' {ManifestFileName} could not be parsed");
        }

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            throw new InvalidDataException($"bundle '{sourceLabel}' manifest has no id");
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            throw new InvalidDataException($"bundle '{manifest.Id}' manifest has no entryAssembly");
        }

        return manifest;
    }

    private static byte[]? ReadEntryBytes(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path.ToNormalizedBundlePath());
        if (entry is null)
        {
            return null;
        }

        using var entryStream = entry.Open();
        using var buffer = new MemoryStream();
        entryStream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static IReadOnlyDictionary<string, byte[]> ReadFolder(ZipArchive archive, string folder)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return result;
        }

        var prefix = folder.ToNormalizedBundlePath().TrimEnd('/') + "/";
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || name.EndsWith('/'))
            {
                continue;
            }

            var relative = name[prefix.Length..];
            using var entryStream = entry.Open();
            using var buffer = new MemoryStream();
            entryStream.CopyTo(buffer);
            result[relative] = buffer.ToArray();
        }

        return result;
    }

    private static IReadOnlyDictionary<string, byte[]> ReadDirectory(string root, string folder)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return result;
        }

        var folderPath = Path.Combine(root, folder);
        if (!Directory.Exists(folderPath))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(folderPath, file).Replace('\\', '/');
            result[relative] = File.ReadAllBytes(file);
        }

        return result;
    }
}
