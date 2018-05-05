using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Localization
{
    public class Layout
    {
        public string LocalizationName { get; set; }
        public Index LocalizationIndex { get; set; }

        public Layout(Dictionary<string, string> set)
        {
            LocalizationIndex = new Index()
            {
                Set = set
            };
        }
    }

    public class Index
    {
        public Dictionary<string, string> Set { get; set; }

        public string this[string key]
        {
            get
            {
                if (!Set.TryGetValue(key, out string value))
                    throw new Exception($"Invalid locale key {key}");
                return value;
            }
        }
    }

}
