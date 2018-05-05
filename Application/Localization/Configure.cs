using SharedLibraryCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace IW4MAdmin.Application.Localization
{
    public class Configure
    {
        public static void Initialize(string customLocale)
        {
            string currentLocale = string.IsNullOrEmpty(customLocale) ? CultureInfo.CurrentCulture.Name : customLocale;
            string[] localizationFiles = Directory.GetFiles("Localization", $"*.{currentLocale}.json");

            // culture doesn't exist so we just want language
            if (localizationFiles.Length == 0)
            {
                localizationFiles = Directory.GetFiles("Localization", $"*.{currentLocale.Substring(0, 2)}*.json");
            }

            // language doesn't exist either so defaulting to english
            if (localizationFiles.Length == 0)
            {
                localizationFiles = Directory.GetFiles("Localization", "*.en-US.json");
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
                        Program.ServerManager.GetLogger().WriteError($"Could not add locale string {item.Key} to localization");
                    }
                }
            }

            string localizationFile = $"Localization{Path.DirectorySeparatorChar}IW4MAdmin.{currentLocale}-{currentLocale.ToUpper()}.json";

            Utilities.CurrentLocalization = new SharedLibraryCore.Localization.Layout(localizationDict)
            {
                LocalizationName = currentLocale,
            };
        }
    }
}
