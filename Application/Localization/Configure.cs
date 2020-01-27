using IW4MAdmin.Application.API.Master;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace IW4MAdmin.Application.Localization
{
    public class Configure
    {
        public static ITranslationLookup Initialize(bool useLocalTranslation, string customLocale = null)
        {
            string currentLocale = string.IsNullOrEmpty(customLocale) ? CultureInfo.CurrentCulture.Name : customLocale;
            string[] localizationFiles = Directory.GetFiles(Path.Join(Utilities.OperatingDirectory, "Localization"), $"*.{currentLocale}.json");

            if (!useLocalTranslation)
            {
                try
                {
                    var api = Endpoint.Get();
                    var localization = api.GetLocalization(currentLocale).Result;
                    Utilities.CurrentLocalization = localization;
                    return localization.LocalizationIndex;
                }

                catch (Exception)
                {
                    // the online localization failed so will default to local files
                }
            }

            // culture doesn't exist so we just want language
            if (localizationFiles.Length == 0)
            {
                localizationFiles = Directory.GetFiles(Path.Join(Utilities.OperatingDirectory, "Localization"), $"*.{currentLocale.Substring(0, 2)}*.json");
            }

            // language doesn't exist either so defaulting to english
            if (localizationFiles.Length == 0)
            {
                localizationFiles = Directory.GetFiles(Path.Join(Utilities.OperatingDirectory, "Localization"), "*.en-US.json");
            }

            // this should never happen unless the localization folder is empty
            if (localizationFiles.Length == 0)
            {
                throw new Exception("No localization files were found");
            }

            var localizationDict = new Dictionary<string, string>();

            foreach (string filePath in localizationFiles)
            {
                var localizationContents = File.ReadAllText(filePath, Encoding.UTF8);
                var eachLocalizationFile = Newtonsoft.Json.JsonConvert.DeserializeObject<SharedLibraryCore.Localization.Layout>(localizationContents);

                foreach (var item in eachLocalizationFile.LocalizationIndex.Set)
                {
                    if (!localizationDict.TryAdd(item.Key, item.Value))
                    {
                        Program.ServerManager.GetLogger(0).WriteError($"Could not add locale string {item.Key} to localization");
                    }
                }
            }

            string localizationFile = $"{Path.Join(Utilities.OperatingDirectory, "Localization")}{Path.DirectorySeparatorChar}IW4MAdmin.{currentLocale}-{currentLocale.ToUpper()}.json";

            Utilities.CurrentLocalization = new SharedLibraryCore.Localization.Layout(localizationDict)
            {
                LocalizationName = currentLocale,
            };

            return Utilities.CurrentLocalization.LocalizationIndex;
        }
    }
}
