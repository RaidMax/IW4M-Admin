using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using SharedLibraryCore.Interfaces;
using System.Linq;
using SharedLibraryCore;
using IW4MAdmin.Application.Misc;

namespace IW4MAdmin.Application.Helpers
{
    /// <summary>
    /// implementation of IPluginImporter
    /// discovers plugins and script plugins
    /// </summary>
    public class PluginImporter : IPluginImporter
    {
        private static readonly string PLUGIN_DIR = "Plugins";
        private readonly ILogger _logger;

        public PluginImporter(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// discovers all the script plugins in the plugins dir
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPlugin> DiscoverScriptPlugins()
        {
            string pluginDir = $"{Utilities.OperatingDirectory}{PLUGIN_DIR}{Path.DirectorySeparatorChar}";

            if (Directory.Exists(pluginDir))
            {
                string[] scriptPluginFiles = Directory.GetFiles(pluginDir, "*.js");

                _logger.WriteInfo($"Discovered {scriptPluginFiles.Length} potential script plugins");

                if (scriptPluginFiles.Length > 0)
                {
                    foreach (string fileName in scriptPluginFiles)
                    {
                        _logger.WriteInfo($"Discovered script plugin {fileName}");
                        var plugin = new ScriptPlugin(fileName);
                        yield return plugin;
                    }
                }
            }
        }

        /// <summary>
        /// discovers all the C# assembly plugins and commands
        /// </summary>
        /// <returns></returns>
        public (IEnumerable<Type>, IEnumerable<Type>) DiscoverAssemblyPluginImplementations()
        {
            string pluginDir = $"{Utilities.OperatingDirectory}{PLUGIN_DIR}{Path.DirectorySeparatorChar}";
            var pluginTypes = Enumerable.Empty<Type>();
            var commandTypes = Enumerable.Empty<Type>();

            if (Directory.Exists(pluginDir))
            {
                var dllFileNames = Directory.GetFiles(pluginDir, "*.dll");
                _logger.WriteInfo($"Discovered {dllFileNames.Length} potential plugin assemblies");

                if (dllFileNames.Length > 0)
                {
                    var assemblies = dllFileNames.Select(_name => Assembly.LoadFrom(_name));

                    pluginTypes = assemblies
                        .SelectMany(_asm => _asm.GetTypes())
                        .Where(_assemblyType => _assemblyType.GetInterface(nameof(IPlugin), false) != null);

                    _logger.WriteInfo($"Discovered {pluginTypes.Count()} plugin implementations");

                    commandTypes = assemblies
                        .SelectMany(_asm => _asm.GetTypes())
                        .Where(_assemblyType => _assemblyType.IsClass && _assemblyType.BaseType == typeof(Command));

                    _logger.WriteInfo($"Discovered {commandTypes.Count()} plugin commands");
                }
            }

            return (pluginTypes, commandTypes);
        }
    }
}
