using SharedLibraryCore.Interfaces;
using System.Collections.Generic;
using System.Globalization;

namespace SharedLibraryCore.Localization
{
    public class Layout
    {
        private string localizationName;
        public string LocalizationName
        {
            get => localizationName;
            set
            {
                localizationName = value;
                Culture = new CultureInfo(value);
            }
        }
        public TranslationLookup LocalizationIndex { get; set; }
        public CultureInfo Culture { get; private set; }

        public Layout(Dictionary<string, string> set)
        {
            LocalizationIndex = new TranslationLookup()
            {
                Set = set
            };
        }
    }

    public class TranslationLookup : ITranslationLookup
    {
        public Dictionary<string, string> Set { get; set; }

        public string this[string key]
        {
            get
            {
                if (!Set.TryGetValue(key, out string value))
                {
                    return key;
                }
                return value;
            }
        }
    }

}
