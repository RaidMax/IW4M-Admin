using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Configuration
{
    public class DefaultConfiguration : IBaseConfiguration
    {
        public int AutoMessagePeriod { get; set; }
        public List<string> AutoMessages { get; set; }
        public List<string> GlobalRules { get; set; }
        public List<MapConfiguration> Maps { get; set; }

        public IBaseConfiguration Generate() => this;

        public string Name() => "DefaultConfiguration";
    }
}
