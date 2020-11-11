using IW4MAdmin.Application.API.Master;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Configuration;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Localization
{
    public static class Configure
    {
        public static ITranslationLookup Initialize(ILogger logger, IMasterApi apiInstance, ApplicationConfiguration applicationConfiguration)
        {
            var useLocalTranslation = applicationConfiguration?.UseLocalTranslations ?? true;
            var customLocale = applicationConfiguration?.EnableCustomLocale ?? false
                ? (applicationConfiguration.CustomLocale ?? "en-US")
                : "en-US";
            var currentLocale = string.IsNullOrEmpty(customLocale) ? CultureInfo.CurrentCulture.Name : customLocale;
            var localizationFiles = Directory.GetFiles(Path.Join(Utilities.OperatingDirectory, "Localization"), $"*.{currentLocale}.json");

            if (!useLocalTranslation)
            {
                try
                {
                    var localization = apiInstance.GetLocalization(currentLocale).Result;
                    Utilities.CurrentLocalization = localization;
                    return localization.LocalizationIndex;
                }

                catch (Exception ex)
                {
                    // the online localization failed so will default to local files
                    logger.LogWarning(ex, "Could not download latest translations");
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
                        logger.LogError("Could not add locale string {key} to localization", item.Key);
                    }
                }
            }

            Utilities.CurrentLocalization = new SharedLibraryCore.Localization.Layout(localizationDict)
            {
                LocalizationName = currentLocale,
            };

            return Utilities.CurrentLocalization.LocalizationIndex;
        }
    }
}
