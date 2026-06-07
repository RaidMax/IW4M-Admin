using System;
using System.Collections.Generic;
using System.Reflection;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     defines the capabilities of the plugin importer
    /// </summary>
    public interface IPluginImporter
    {
        /// <summary>
        ///     discovers C# assembly plugin and command types
        /// </summary>
        /// <returns>tuple of IPlugin implementation type definitions, and IManagerCommand type definitions</returns>
        (IEnumerable<Type>, IEnumerable<Type>, IEnumerable<Type>) DiscoverAssemblyPluginImplementations();

        /// <summary>
        ///     the distilled set of plugin assemblies (loose DLLs, local and remote bundles, remote binaries),
        ///     deduped to the highest version of each and cached after the first call. Lets the webfront
        ///     register plugin MVC parts and Blazor components from the same source the importer uses.
        /// </summary>
        IEnumerable<Assembly> DiscoverPluginAssemblies();

        /// <summary>
        ///     discovers the script plugins
        /// </summary>
        /// <returns>initialized script plugin collection</returns>
        IEnumerable<(Type, string)> DiscoverScriptPlugins();
    }
}
