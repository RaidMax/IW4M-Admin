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
        public static List<EventNotify> potentialNotifies;

        public static bool Load()
        {
            string[] dllFileNames = null;
            potentialCommands = new List<Command>();
            potentialNotifies = new List<EventNotify>();

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

            foreach (Assembly Plugin in assemblies)
            {
                if (Plugin != null)
                {
                    Type[] types = Plugin.GetTypes();
                    foreach(Type assemblyType in types)
                    {
                        if(assemblyType.IsClass && assemblyType.BaseType.Name == "EventNotify")
                        {
                            Object notifyObject = Activator.CreateInstance(assemblyType);
                            EventNotify newNotify = (EventNotify)notifyObject;
                            potentialNotifies.Add(newNotify);
                            newNotify.onLoad();
                            Program.getManager().mainLog.Write("Loaded event plugin \"" + assemblyType.Name + "\"", Log.Level.All);
                        }

                        else if (assemblyType.IsClass && assemblyType.BaseType.Name == "Command")
                        {
                            Object commandObject = Activator.CreateInstance(assemblyType);
                            Command newCommand = (Command)commandObject;
                            potentialCommands.Add(newCommand);
                            Program.getManager().mainLog.Write("Loaded command plugin \"" + newCommand.Name + "\"", Log.Level.All);
                        }

                        else
                            Program.getManager().mainLog.Write("Ignoring invalid plugin \"" + assemblyType.Name + "\"", Log.Level.All);
                    }
                }
            }
            return true;
        }
    }
}
