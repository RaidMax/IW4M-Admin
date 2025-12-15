using SharedLibraryCore;
using System;
using System.IO;

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

        public static void ModifyLogPath020919(SharedLibraryCore.Configuration.ServerConfiguration config)
        {
            if (config.ManualLogPath.IsRemoteLog())
            {
                config.GameLogServerUrl = new Uri(config.ManualLogPath);
                config.ManualLogPath = null;
            }
        }

        public static void UpdatePlutoniumT6Parser(SharedLibraryCore.Configuration.ServerConfiguration config)
        {
            if (config.RConParserVersion != "Plutonium T6 Parser")
            {
                return;
            }

            if (!"Plutonium T6 parser requires an update. Would you like to automatically update now".PromptBool())
            {
                return;
            }

            config.RConParserVersion = "Plutonium T6 Parser (2024)";
            config.EventParserVersion = "Plutonium T6 Parser (2024)";
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
