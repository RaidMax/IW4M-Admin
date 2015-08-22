using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using SharedLibrary;

namespace IW4MAdmin
{
    public class PluginImporter
    {
        public static List<Command> potentialCommands = new List<Command>();
        public static List<Plugin> potentialPlugins = new List<Plugin>();

        public static bool Load()
        {
            string[] dllFileNames = null;

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
                byte[] rawDLL = File.ReadAllBytes(dllFile); // because we want to update the plugin without restarting
                Assembly assembly = Assembly.Load(rawDLL);
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
                            potentialPlugins.Add(newNotify);

                            try
                            {
                                newNotify.onLoad();
                            }

                            catch (Exception E)
                            {
                                Program.getManager().mainLog.Write("There was an error starting \"" + newNotify.Name + "\" plugin", Log.Level.Debug);
                                Program.getManager().mainLog.Write("Error Message: " + E.Message, Log.Level.Debug);
                                Program.getManager().mainLog.Write("Error Trace: " + E.StackTrace, Log.Level.Debug);
                                continue;
                            }
                            
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

        public static void Unload()
        {
            foreach (Plugin P in potentialPlugins)
            {
                try
                {
                    P.onUnload();
                }

                catch (Exception E)
                {
                    Program.getManager().mainLog.Write("There was an error unloading \"" + P.Name + "\" plugin", Log.Level.Debug);
                    Program.getManager().mainLog.Write("Error Message: " + E.Message, Log.Level.Debug);
                    Program.getManager().mainLog.Write("Error Trace: " + E.StackTrace, Log.Level.Debug);
                    continue;
                }
            }

            potentialCommands = new List<Command>();
            potentialPlugins = new List<Plugin>();

        }
    }
}
