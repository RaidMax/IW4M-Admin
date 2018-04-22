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
            string currentLocal = CultureInfo.CurrentCulture.Name;
            string localizationFile = $"Localization{Path.DirectorySeparatorChar}IW4MAdmin.{currentLocal}.json";
            string localizationContents;

            if (File.Exists(localizationFile))
            {
                localizationContents = File.ReadAllText(localizationFile);
               
            }

            else
            {
                localizationFile = $"Localization{Path.DirectorySeparatorChar}IW4MAdmin.en-US.json";
                localizationContents = File.ReadAllText(localizationFile);
            }

            Utilities.CurrentLocalization = Newtonsoft.Json.JsonConvert.DeserializeObject<SharedLibraryCore.Localization.Layout>(localizationContents);
        }
    }
}
