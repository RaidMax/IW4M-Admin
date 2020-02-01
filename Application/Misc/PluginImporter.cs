using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using SharedLibraryCore.Interfaces;
using System.Linq;
using SharedLibraryCore;

namespace IW4MAdmin.Application.Helpers
{
    public class PluginImporter : IPluginImporter
    {
        public IList<Type> CommandTypes { get; private set; } = new List<Type>();
        public IList<IPlugin> ActivePlugins { get; private set; } = new List<IPlugin>();
        public IList<Assembly> PluginAssemblies { get; private set; } = new List<Assembly>();
        public IList<Assembly> Assemblies { get; private set; } = new List<Assembly>();

        private readonly ILogger _logger;
        private readonly ITranslationLookup _translationLookup;

        public PluginImporter(ILogger logger, ITranslationLookup translationLookup)
        {
            _logger = logger;
            _translationLookup = translationLookup;
        }

        /// <summary>
        /// Loads all the assembly and javascript plugins
        /// </summary>
        public void Load()
        {
            string pluginDir = $"{Utilities.OperatingDirectory}Plugins{Path.DirectorySeparatorChar}";
            string[] dllFileNames = null;
            string[] scriptFileNames = null;

            if (Directory.Exists(pluginDir))
            {
                dllFileNames = Directory.GetFiles($"{Utilities.OperatingDirectory}Plugins{Path.DirectorySeparatorChar}", "*.dll");
                scriptFileNames = Directory.GetFiles($"{Utilities.OperatingDirectory}Plugins{Path.DirectorySeparatorChar}", "*.js");
            }

            else
            {
                dllFileNames = new string[0];
                scriptFileNames = new string[0];
            }

            if (dllFileNames.Length == 0 &&
                scriptFileNames.Length == 0)
            {
                _logger.WriteDebug(_translationLookup["PLUGIN_IMPORTER_NOTFOUND"]);
                return;
            }

            // load up the script plugins
            foreach (string fileName in scriptFileNames)
            {
                var plugin = new ScriptPlugin(fileName);
                _logger.WriteDebug($"Loaded script plugin \"{ plugin.Name }\" [{plugin.Version}]");
                ActivePlugins.Add(plugin);
            }

            ICollection<Assembly> assemblies = new List<Assembly>(dllFileNames.Length);
            foreach (string dllFile in dllFileNames)
            {
                assemblies.Add(Assembly.LoadFrom(dllFile));
            }

            int LoadedCommands = 0;
            foreach (Assembly Plugin in assemblies)
            {
                if (Plugin != null)
                {
                    Assemblies.Add(Plugin);
                    Type[] types = Plugin.GetTypes();
                    foreach (Type assemblyType in types)
                    {
                        if (assemblyType.IsClass && assemblyType.BaseType == typeof(Command))
                        {
                            CommandTypes.Add(assemblyType);
                            _logger.WriteDebug($"{_translationLookup["PLUGIN_IMPORTER_REGISTERCMD"]} \"{assemblyType.Name}\"");
                            LoadedCommands++;
                            continue;
                        }

                        try
                        {
                            if (assemblyType.GetInterface("IPlugin", false) == null)
                                continue;

                            var notifyObject = Activator.CreateInstance(assemblyType);
                            IPlugin newNotify = (IPlugin)notifyObject;
                            if (ActivePlugins.FirstOrDefault(x => x.Name == newNotify.Name) == null)
                            {
                                ActivePlugins.Add(newNotify);
                                PluginAssemblies.Add(Plugin);
                                _logger.WriteDebug($"Loaded plugin \"{newNotify.Name}\" [{newNotify.Version}]");
                            }
                        }

                        catch (Exception e)
                        {
                            _logger.WriteWarning(_translationLookup["PLUGIN_IMPORTER_ERROR"].FormatExt(Plugin.Location));
                            _logger.WriteDebug(e.GetExceptionInfo());
                        }
                    }
                }
            }

            _logger.WriteInfo($"Loaded {ActivePlugins.Count} plugins and registered {LoadedCommands} plugin commands.");
        }
    }
}
