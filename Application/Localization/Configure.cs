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
        public static void Initialize()
        {
            string currentLocale = Program.ServerManager.GetApplicationSettings().Configuration().CustomLocale ?? 
                CultureInfo.CurrentCulture.Name.Substring(0, 2);
#if DEBUG
 //           currentLocal = "ru-RU";
#endif
            string localizationFile = $"Localization{Path.DirectorySeparatorChar}IW4MAdmin.{currentLocale}-{currentLocale.ToUpper()}.json";
            string localizationContents;

            if (File.Exists(localizationFile))
            {
                localizationContents = File.ReadAllText(localizationFile, Encoding.UTF8);
            }

            else
            {
                localizationFile = $"Localization{Path.DirectorySeparatorChar}IW4MAdmin.en-US.json";
                localizationContents = File.ReadAllText(localizationFile, Encoding.UTF8);
            }

            Utilities.CurrentLocalization = Newtonsoft.Json.JsonConvert.DeserializeObject<SharedLibraryCore.Localization.Layout>(localizationContents);
        }
    }
}
