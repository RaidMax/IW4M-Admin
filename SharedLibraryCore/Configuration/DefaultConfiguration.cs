using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Configuration
{
    public class DefaultConfiguration : IBaseConfiguration
    {
        public string[] AutoMessages { get; set; }
        public string[] GlobalRules { get; set; }
        public MapConfiguration[] Maps { get; set; }
        public QuickMessageConfiguration[] QuickMessages {get; set;}
        public string[] DisallowedClientNames { get; set; }

        public IBaseConfiguration Generate() => this;

        public string Name() => "DefaultConfiguration";
    }
}
