using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IW4MAdmin.Application.Migration
{
    /// <summary>
    /// helps facilitate the migration of configs from one version and or location
    /// to another
    /// </summary>
    class ConfigurationMigration
    {
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
                    log.WriteDebug($"Creating directory for configs {configDirectory}");
                    Directory.CreateDirectory(configDirectory);
                }

                var configurationFiles = Directory.EnumerateFiles(currentDirectory, "*.json")
                    .Select(f => f.Split(Path.DirectorySeparatorChar).Last())
                    .Where(f => f.Count(c => c == '.') == 1);

                foreach (var configFile in configurationFiles)
                {
                    log.WriteDebug($"Moving config file {configFile}");
                    string destinationPath = Path.Join("Configuration", configFile);
                    if (!File.Exists(destinationPath))
                    {
                        File.Move(configFile, destinationPath);
                    }
                }
            }
        }
    }
}
