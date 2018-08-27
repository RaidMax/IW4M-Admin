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

        public static bool Load(IManager Manager)
        {
            string[] dllFileNames = Directory.GetFiles($"{Utilities.OperatingDirectory}Plugins{Path.DirectorySeparatorChar}", "*.dll");
            string[] scriptFileNames = Directory.GetFiles($"{Utilities.OperatingDirectory}Plugins{Path.DirectorySeparatorChar}", "*.js");

            if (dllFileNames.Length == 0 &&
                scriptFileNames.Length == 0)
            {
                Manager.GetLogger().WriteDebug(Utilities.CurrentLocalization.LocalizationIndex["PLUGIN_IMPORTER_NOTFOUND"]);
                return true;
            }

            // load up the script plugins
            foreach (string fileName in scriptFileNames)
            {
                var plugin = new ScriptPlugin(fileName);
                plugin.Initialize(Manager).Wait();
                Manager.GetLogger().WriteDebug($"Loaded script plugin \"{ plugin.Name }\" [{plugin.Version}]");
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
                    Type[] types = Plugin.GetTypes();
                    foreach (Type assemblyType in types)
                    {
                        if (assemblyType.IsClass && assemblyType.BaseType.Name == "Command")
                        {
                            Object commandObject = Activator.CreateInstance(assemblyType);
                            Command newCommand = (Command)commandObject;
                            ActiveCommands.Add(newCommand);
                            Manager.GetLogger().WriteDebug($"{Utilities.CurrentLocalization.LocalizationIndex["PLUGIN_IMPORTER_REGISTERCMD"]} \"{newCommand.Name}\"");
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
                                Manager.GetLogger().WriteDebug($"Loaded plugin \"{ newNotify.Name }\" [{newNotify.Version}]");
                            }
                        }

                        catch (Exception E)
                        {
                            Manager.GetLogger().WriteWarning($"{Utilities.CurrentLocalization.LocalizationIndex["PLUGIN_IMPORTER_ERROR"]} {Plugin.Location} - {E.Message}");
                        }
                    }
                }
            }
            Manager.GetLogger().WriteInfo($"Loaded {ActivePlugins.Count} plugins and registered {LoadedCommands} commands.");
            return true;
        }
    }
}
