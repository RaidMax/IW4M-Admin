using SharedLibraryCore;
using SharedLibraryCore.Plugins;
using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

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

        /// <summary>
        /// disables .js script plugins that have been replaced by .cs versions
        /// </summary>
        public static void MigrateJsToCsPlugins()
        {
            var pluginsDir = Path.Join(Utilities.OperatingDirectory, "Plugins");

            if (!Directory.Exists(pluginsDir))
            {
                return;
            }

            var csFiles = Directory.GetFiles(pluginsDir, "*.cs");

            foreach (var csFile in csFiles)
            {
                var jsFile = Path.ChangeExtension(csFile, ".js");

                if (!File.Exists(jsFile))
                {
                    continue;
                }

                try
                {
                    File.Move(jsFile, jsFile + ".disabled", overwrite: true);
                    Console.WriteLine($"Migrated plugin: {Path.GetFileNameWithoutExtension(jsFile)}.js → .cs");
                }
                catch (IOException)
                {
                    // best effort — file may be locked
                }
            }
        }

        /// <summary>
        /// disables loose plugin .dll files that have been superseded by a .zip bundle shipping the same
        /// entry assembly. Mirrors <see cref="MigrateJsToCsPlugins"/>: when a plugin moves to its bundle
        /// form (e.g. LiveRadar), an older loose dll left in Plugins/ from a prior install would double-load
        /// against the in-memory bundle assembly (duplicate identity / conflicting types). Renaming it to
        /// .dll.disabled lets the bundle win. The match is driven by the bundle manifest's entryAssembly,
        /// not the file names, so it stays correct even if the .zip and .dll stems differ. Best-effort:
        /// bundles whose manifest can't be read, and locked dlls, are skipped.
        /// </summary>
        public static void MigrateDllToBundlePlugins()
        {
            var pluginsDir = Path.Join(Utilities.OperatingDirectory, "Plugins");

            if (!Directory.Exists(pluginsDir))
            {
                return;
            }

            foreach (var zipFile in Directory.GetFiles(pluginsDir, "*.zip"))
            {
                string? entryAssemblyFile;

                try
                {
                    using var archive = ZipFile.OpenRead(zipFile);
                    var manifestEntry = archive.GetEntry("manifest.json");

                    if (manifestEntry is null)
                    {
                        continue;
                    }

                    byte[] manifestBytes;
                    using (var entryStream = manifestEntry.Open())
                    using (var buffer = new MemoryStream())
                    {
                        entryStream.CopyTo(buffer);
                        manifestBytes = buffer.ToArray();
                    }

                    var manifest = JsonSerializer.Deserialize<BundleManifest>(manifestBytes,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (string.IsNullOrWhiteSpace(manifest?.EntryAssembly))
                    {
                        continue;
                    }

                    // entryAssembly is a bundle-relative path like "lib/LiveRadar.dll" — take just the file name.
                    entryAssemblyFile = Path.GetFileName(manifest.EntryAssembly.Replace('\\', '/'));
                }
                catch (Exception)
                {
                    // unreadable / corrupt bundle — leave any loose dll untouched
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entryAssemblyFile))
                {
                    continue;
                }

                var looseDll = Path.Join(pluginsDir, entryAssemblyFile);

                if (!File.Exists(looseDll))
                {
                    continue;
                }

                try
                {
                    File.Move(looseDll, looseDll + ".disabled", overwrite: true);
                    Console.WriteLine($"Migrated plugin: {entryAssemblyFile} → bundle ({Path.GetFileName(zipFile)})");
                }
                catch (IOException)
                {
                    // best effort — file may be locked
                }
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
