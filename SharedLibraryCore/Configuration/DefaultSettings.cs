using System;
using System.Collections.Generic;
using System.Linq;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Configuration
{
    public class DefaultSettings : IBaseConfiguration
    {
        public string[] AutoMessages { get; set; }
        public string[] GlobalRules { get; set; }
        public MapConfiguration[] Maps { get; set; }
        public GametypeConfiguration[] Gametypes { get; set; }
        public QuickMessageConfiguration[] QuickMessages { get; set; }
        public string[] DisallowedClientNames { get; set; }
        public GameStringConfiguration GameStrings { get; set; }

        private Dictionary<string, string> _mapDisplayNameCache;

        /// <summary>
        /// Returns the display name for a map code name by searching across all configured games.
        /// Falls back to the original name with underscores replaced by spaces if not found.
        /// </summary>
        public string GetMapDisplayName(string codeName)
        {
            if (string.IsNullOrEmpty(codeName))
            {
                return "Unknown";
            }

            _mapDisplayNameCache ??= Maps?
                .Where(mc => mc.Maps is not null)
                .SelectMany(mc => mc.Maps)
                .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Alias, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            return _mapDisplayNameCache.TryGetValue(codeName, out var displayName)
                ? displayName
                : codeName.Replace("_", " ");
        }

        public IBaseConfiguration Generate()
        {
            return this;
        }

        public string Name()
        {
            return "DefaultConfiguration";
        }
    }
}