using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Plugin;

/// <summary>
/// Default <see cref="IPluginDataStore{TPlugin}"/> implementation. Resolves the folder from the
/// plugin's own assembly (<see cref="PluginDirectoryResolver"/>) and lazily creates it. Origin of the
/// assembly is irrelevant — disk DLL, in-memory compiled <c>.cs</c> script, local/remote <c>.zip</c>
/// bundle, or a binary streamed from the remote store all resolve by assembly name alone.
/// </summary>
public sealed class PluginDataStore<TPlugin> : IPluginDataStore<TPlugin>
{
    private readonly Lazy<string> _root = new(() =>
    {
        var path = PluginDirectoryResolver.ResolveDirectory(typeof(TPlugin).Assembly);
        Directory.CreateDirectory(path);
        return path;
    });

    public string RootPath => _root.Value;

    public string GetPath(params string[] segments)
    {
        var rootFull = Path.GetFullPath(RootPath);

        if (segments is null || segments.Length == 0)
        {
            return rootFull;
        }

        var combined = Path.GetFullPath(Path.Combine(new[] { rootFull }.Concat(segments).ToArray()));

        // reject anything that resolves outside the plugin folder (e.g. a "../" segment)
        if (combined != rootFull &&
            !combined.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resolved path '{combined}' escapes the plugin data directory '{rootFull}'.");
        }

        var directory = Path.GetDirectoryName(combined);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return combined;
    }

    public async Task<string> EnsureAsset(string embeddedResourceName, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(embeddedResourceName))
        {
            throw new ArgumentException("Resource name cannot be null or empty.", nameof(embeddedResourceName));
        }

        var target = GetPath(embeddedResourceName.Split(SharedLibraryCore.Utilities.DirectorySeparatorChars));

        if (File.Exists(target) && !overwrite)
        {
            return target;
        }

        var assembly = typeof(TPlugin).Assembly;
        await using var resourceStream = ResolveManifestResource(assembly, embeddedResourceName)
            ?? throw new FileNotFoundException(
                $"Embedded resource '{embeddedResourceName}' was not found in assembly '{assembly.GetName().Name}'.");

        await using var outputStream = File.Create(target);
        await resourceStream.CopyToAsync(outputStream);
        return target;
    }

    /// <summary>
    /// Looks up an embedded resource by exact manifest name first, then by suffix (manifest names are
    /// namespace-prefixed and dot-separated, so an author-friendly "folder/file" rarely matches exactly).
    /// </summary>
    private static Stream ResolveManifestResource(Assembly assembly, string name)
    {
        var direct = assembly.GetManifestResourceStream(name);
        if (direct is not null)
        {
            return direct;
        }

        var normalized = name.Replace('/', '.').Replace('\\', '.');
        var match = assembly.GetManifestResourceNames()
            .FirstOrDefault(candidate => candidate.EndsWith(normalized, StringComparison.OrdinalIgnoreCase));

        return match is null ? null : assembly.GetManifestResourceStream(match);
    }
}
