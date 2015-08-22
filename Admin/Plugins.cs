using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using SharedLibrary;

namespace IW4MAdmin
{
    public class PluginImporter
    {
        public static List<Command> potentialCommands;
        public static List<Plugin> potentialNotifies;

        public static bool Load()
        {
            string[] dllFileNames = null;
            potentialCommands = new List<Command>();
            potentialNotifies = new List<Plugin>();

            if (Directory.Exists(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\plugins"))
                dllFileNames = Directory.GetFiles(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\plugins", "*.dll");

            else
            {
                Program.getManager().mainLog.Write("Plugin folder does not exist!", Log.Level.All);
                return false;
            }

            if (dllFileNames == null || dllFileNames.Length == 0)
            {
                Program.getManager().mainLog.Write("No plugins to load", Log.Level.All);
                return true;
            }

            ICollection<Assembly> assemblies = new List<Assembly>(dllFileNames.Length);
            foreach (string dllFile in dllFileNames)
            {
                AssemblyName an = AssemblyName.GetAssemblyName(dllFile);
                Assembly assembly = Assembly.Load(an);
                assemblies.Add(assembly);
            }

            int totalLoaded = 0;
            foreach (Assembly Plugin in assemblies)
            {
                if (Plugin != null)
                {
                    Type[] types = Plugin.GetTypes();
                    foreach(Type assemblyType in types)
                    {
                        if(assemblyType.IsClass && assemblyType.BaseType.Name == "Plugin")
                        {
                            Object notifyObject = Activator.CreateInstance(assemblyType);
                            Plugin newNotify = (Plugin)notifyObject;
                            potentialNotifies.Add(newNotify);
                            newNotify.onLoad();
                            Program.getManager().mainLog.Write("Loaded plugin \"" + newNotify.Name + "\"" + " [" + newNotify.Version + "]", Log.Level.Debug);
                            totalLoaded++;
                        }

                        else if (assemblyType.IsClass && assemblyType.BaseType.Name == "Command")
                        {
                            Object commandObject = Activator.CreateInstance(assemblyType);
                            Command newCommand = (Command)commandObject;
                            potentialCommands.Add(newCommand);
                            Program.getManager().mainLog.Write("Registered command \"" + newCommand.Name + "\"", Log.Level.Debug);
                            totalLoaded++;
                        }
                    }
                }
            }

            Program.getManager().mainLog.Write("Loaded " + totalLoaded + " plugins.", Log.Level.Production);
            return true;
        }
    }
}
