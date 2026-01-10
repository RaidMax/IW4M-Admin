using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace IW4MAdmin.Application.Plugin.CSharpScript;

/// <summary>
/// A collectible AssemblyLoadContext that enables unloading of loaded assemblies.
/// Shares core assemblies with the host to maintain type compatibility.
/// </summary>
public class CsPluginLoadContext() : AssemblyLoadContext(isCollectible: true)
{
    /// <summary>
    /// Assemblies that should be loaded from the Default context to maintain type compatibility.
    /// </summary>
    private static readonly string[] SharedAssemblyPrefixes =
    [
        "SharedLibraryCore",
        "Data",
        "Stats",
        "Microsoft.Extensions",
        "Microsoft.EntityFrameworkCore",
        "System",
        "netstandard",
        "mscorlib"
    ];

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Share core assemblies with host for type compatibility
        // Returning null causes the runtime to load from the Default context
        return SharedAssemblyPrefixes.Any(prefix => assemblyName.Name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ?? false)
            ? null
            // Plugin-specific assemblies stay isolated in this context
            : base.Load(assemblyName);
    }
}
