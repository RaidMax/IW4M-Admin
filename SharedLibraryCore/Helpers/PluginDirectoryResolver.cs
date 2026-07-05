using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SharedLibraryCore.Helpers;

/// <summary>
/// Resolves the on-disk data directory for a plugin from a type it defines. Identity comes from
/// <c>typeof(T).Assembly.GetName().Name</c> — the host's canonical plugin identifier — so it works
/// uniformly across every plugin format: compiled <c>.dll</c>, in-memory compiled <c>.cs</c> scripts
/// (assembly named after the file), and <c>.zip</c> bundles (the bundle's compiled assembly).
///
/// Routing is by elimination: a type defined in a host/framework assembly keeps the legacy flat
/// <c>Configuration/</c> behavior; everything else is a plugin and routes to <c>Plugins/&lt;key&gt;/</c>.
/// This avoids depending on membership in any discovery set (script assemblies are not enumerated
/// there).
/// </summary>
public static class PluginDirectoryResolver
{
    /// <summary>
    /// Framework/host assemblies whose types are *not* plugins. Types defined here keep the legacy
    /// flat configuration location. Anything outside this set is treated as a plugin.
    /// </summary>
    private static readonly HashSet<string> HostAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "IW4MAdmin",
        "SharedLibraryCore",
        "Data",
        "WebfrontCore",
        "WebCommon",
        "Integrations.Cod",
        "Integrations.Source"
    };

    /// <summary>
    /// True when the assembly is a host/framework assembly (its types route to the legacy flat
    /// configuration directory rather than a per-plugin folder).
    /// </summary>
    public static bool IsHostAssembly(Assembly assembly) =>
        HostAssemblies.Contains(assembly.GetName().Name ?? string.Empty);

    /// <summary>
    /// The sanitized folder key for a plugin assembly (the DLL / script-file / bundle assembly name).
    /// </summary>
    public static string ResolveKey(Assembly assembly) => Sanitize(assembly.GetName().Name ?? "Unknown");

    private static readonly ConcurrentDictionary<Assembly, bool> PluginAssemblyCache = new();

    /// <summary>
    /// True when the assembly actually contains a plugin (an <c>IPluginV2</c>/<c>IPlugin</c>
    /// implementation). A genuine plugin — compiled, scripted, bundled, or streamed — always defines its
    /// own plugin type here, whereas a shared helper library referenced by several plugins does not. Used
    /// to warn (but not block) when a config/context type lives outside any real plugin, since its folder
    /// key would then be the shared library rather than a single plugin.
    /// </summary>
    public static bool LooksLikePluginAssembly(Assembly assembly) =>
        PluginAssemblyCache.GetOrAdd(assembly, static asm =>
        {
            try
            {
                return asm.GetTypes().Any(type =>
                    type is { IsInterface: false, IsAbstract: false } &&
                    (type.GetInterface("IPluginV2") is not null || type.GetInterface("IPlugin") is not null));
            }
            catch
            {
                // could not introspect the assembly; assume it is fine and stay quiet
                return true;
            }
        });

    /// <summary>
    /// Absolute path to a plugin's data folder: <c>Plugins/&lt;key&gt;/</c>, co-located with the binary.
    /// </summary>
    public static string ResolveDirectory(Assembly assembly) =>
        Path.Combine(Utilities.PluginsDirectory, ResolveKey(assembly));

    /// <summary>
    /// Resolves a file path under <paramref name="root"/> from a caller-supplied name that may contain
    /// forward-slash subfolder segments. Each segment is sanitized, <paramref name="extension"/> is
    /// appended if absent, intermediate directories are created, and the result is constrained to the
    /// root (a name that would escape throws).
    /// </summary>
    public static string CombineWithinRoot(string root, string name, string extension)
    {
        var rootFull = Path.GetFullPath(root);

        var segments = (name ?? string.Empty)
            .Split(Utilities.DirectorySeparatorChars, StringSplitOptions.RemoveEmptyEntries)
            .Select(Sanitize)
            .ToArray();

        if (segments.Length == 0)
        {
            segments = ["Unknown"];
        }

        if (!segments[^1].EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            segments[^1] += extension;
        }

        var combined = Path.GetFullPath(Path.Combine([rootFull, .. segments]));

        if (combined != rootFull &&
            !combined.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Name '{name}' resolves outside '{rootFull}'.");
        }

        var directory = Path.GetDirectoryName(combined);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return combined;
    }

    /// <summary>
    /// Strips characters illegal in a file name and defends against relative-path tokens, collapsing
    /// the result to a safe, stable folder name. Never returns empty.
    /// </summary>
    public static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Unknown";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        // collapse any residual relative-path tokens and avoid leading/trailing dots
        cleaned = cleaned.Replace("..", string.Empty).Trim().Trim('.');
        return string.IsNullOrWhiteSpace(cleaned) ? "Unknown" : cleaned;
    }
}
