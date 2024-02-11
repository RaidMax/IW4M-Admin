using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Localization
{
    public class Layout
    {
        private string localizationName;

        public Layout() { }

        public Layout(Dictionary<string, string> set)
        {
            LocalizationIndex = new TranslationLookup
            {
                Set = set
            };
        }

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
        [JsonIgnore] public CultureInfo Culture { get; private set; }
    }

    public class TranslationLookup : ITranslationLookup
    {
        public Dictionary<string, string> Set { get; set; }

        public string this[string key]
        {
            get
            {
                if (!Set.TryGetValue(key, out var value))
                {
                    return key;
                }

                return value;
            }
        }
    }
}
