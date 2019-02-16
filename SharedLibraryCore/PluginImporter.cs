using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using SharedLibraryCore.Interfaces;
using System.Linq;

namespace SharedLibraryCore.Plugins
{
    public class PluginImporter
    {
        public static List<Command> ActiveCommands = new List<Command>();
        public static List<IPlugin> ActivePlugins = new List<IPlugin>();
        public static List<Assembly> PluginAssemblies = new List<Assembly>();
        public static List<Assembly> Assemblies = new List<Assembly>();

        public static bool Load(IManager Manager)
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
                Manager.GetLogger(0).WriteDebug(Utilities.CurrentLocalization.LocalizationIndex["PLUGIN_IMPORTER_NOTFOUND"]);
                return true;
            }

            // load up the script plugins
            foreach (string fileName in scriptFileNames)
            {
                var plugin = new ScriptPlugin(fileName);
                plugin.Initialize(Manager).Wait();
                Manager.GetLogger(0).WriteDebug($"Loaded script plugin \"{ plugin.Name }\" [{plugin.Version}]");
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
                        if (assemblyType.IsClass && assemblyType.BaseType.Name == "Command")
                        {
                            Object commandObject = Activator.CreateInstance(assemblyType);
                            Command newCommand = (Command)commandObject;
                            ActiveCommands.Add(newCommand);
                            Manager.GetLogger(0).WriteDebug($"{Utilities.CurrentLocalization.LocalizationIndex["PLUGIN_IMPORTER_REGISTERCMD"]} \"{newCommand.Name}\"");
                            LoadedCommands++;
                            continue;
                        }

                        try
                        {
                            if (assemblyType.GetInterface("IPlugin", false) == null)
                                continue;

                            Object notifyObject = Activator.CreateInstance(assemblyType);
                            IPlugin newNotify = (IPlugin)notifyObject;
                            if (ActivePlugins.Find(x => x.Name == newNotify.Name) == null)
                            {
                                ActivePlugins.Add(newNotify);
                                PluginAssemblies.Add(Plugin);
                                Manager.GetLogger(0).WriteDebug($"Loaded plugin \"{ newNotify.Name }\" [{newNotify.Version}]");
                            }
                        }

                        catch (Exception E)
                        {
                            Manager.GetLogger(0).WriteWarning($"{Utilities.CurrentLocalization.LocalizationIndex["PLUGIN_IMPORTER_ERROR"]} {Plugin.Location} - {E.Message}");
                        }
                    }
                }
            }
            Manager.GetLogger(0).WriteInfo($"Loaded {ActivePlugins.Count} plugins and registered {LoadedCommands} commands.");
            return true;
        }
    }
}
