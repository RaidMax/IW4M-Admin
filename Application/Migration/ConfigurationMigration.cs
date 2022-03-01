using SharedLibraryCore;
using System;
using System.IO;
using System.Linq;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Migration
{
    /// <summary>
    /// helps facilitate the migration of configs from one version and or location
    /// to another
    /// </summary>
    class ConfigurationMigration
    {
        /// <summary>
        /// ensures required directories are created
        /// </summary>
        public static void CheckDirectories()
        {
            if (!Directory.Exists(Path.Join(Utilities.OperatingDirectory, "Plugins")))
            {
                Directory.CreateDirectory(Path.Join(Utilities.OperatingDirectory, "Plugins"));
            }

            if (!Directory.Exists(Path.Join(Utilities.OperatingDirectory, "Database")))
            {
                Directory.CreateDirectory(Path.Join(Utilities.OperatingDirectory, "Database"));
            }

            if (!Directory.Exists(Path.Join(Utilities.OperatingDirectory, "Log")))
            {
                Directory.CreateDirectory(Path.Join(Utilities.OperatingDirectory, "Log"));
            }

            if (!Directory.Exists(Path.Join(Utilities.OperatingDirectory, "Localization")))
            {
                Directory.CreateDirectory(Path.Join(Utilities.OperatingDirectory, "Localization"));
            }
        }

        /// <summary>
        /// moves existing configs from the root folder into a configs folder
        /// </summary>
        public static void MoveConfigFolder10518(ILogger log)
        {
            string currentDirectory = Utilities.OperatingDirectory;

            // we don't want to do this for migrations or tests where the 
            // property isn't initialized or it's wrong
            if (currentDirectory != null)
            {
                string configDirectory = Path.Join(currentDirectory, "Configuration");

                if (!Directory.Exists(configDirectory))
                {
                    Directory.CreateDirectory(configDirectory);
                }

                var configurationFiles = Directory.EnumerateFiles(currentDirectory, "*.json")
                    .Select(f => f.Split(Path.DirectorySeparatorChar).Last())
                    .Where(f => f.Count(c => c == '.') == 1);

                foreach (var configFile in configurationFiles)
                {
                    string destinationPath = Path.Join("Configuration", configFile);
                    if (!File.Exists(destinationPath))
                    {
                        File.Move(configFile, destinationPath);
                    }
                }

                if (!File.Exists(Path.Join("Database", "Database.db")) &&
                    File.Exists("Database.db"))
                {
                    File.Move("Database.db", Path.Join("Database", "Database.db"));
                }
            }
        }

        public static void ModifyLogPath020919(SharedLibraryCore.Configuration.ServerConfiguration config)
        {
            if (config.ManualLogPath.IsRemoteLog())
            {
                config.GameLogServerUrl = new Uri(config.ManualLogPath);
                config.ManualLogPath = null;
            }
        }

        public static void RemoveObsoletePlugins20210322()
        {
            var files = new[] {"StatsWeb.dll", "StatsWeb.Views.dll", "IW4ScriptCommands.dll"};

            foreach (var file in files)
            {
                var path = Path.Join(Utilities.OperatingDirectory, "Plugins", file);
                
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
