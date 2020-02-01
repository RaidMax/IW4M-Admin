using System;
using System.Collections.Generic;
using System.Reflection;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// Defines the capabilities of the plugin importer
    /// </summary>
    public interface IPluginImporter
    {
        /// <summary>
        /// Command types that are defined in plugin assemblies
        /// </summary>
        IList<Type> CommandTypes { get; }

        /// <summary>
        /// The loaded plugins from plugin assemblies
        /// </summary>
        IList<IPlugin> ActivePlugins { get; }

        /// <summary>
        /// Assemblies that contain plugins
        /// </summary>
        IList<Assembly> PluginAssemblies { get; }

        /// <summary>
        /// All assemblies in the plugin folder
        /// </summary>
        IList<Assembly> Assemblies { get; }
        
        /// <summary>
        /// Loads in plugin assemblies and script plugins
        /// </summary>
        void Load();
    }
}
