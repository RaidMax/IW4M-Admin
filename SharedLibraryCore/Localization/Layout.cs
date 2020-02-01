using SharedLibraryCore.Interfaces;
using System.Collections.Generic;

namespace SharedLibraryCore.Localization
{
    public class Layout
    {
        public string LocalizationName { get; set; }
        public TranslationLookup LocalizationIndex { get; set; }

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
