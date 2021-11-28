using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Configuration
{
    public class DefaultSettings : IBaseConfiguration
    {
        public string[] AutoMessages { get; set; }
        public string[] GlobalRules { get; set; }
        public MapConfiguration[] Maps { get; set; }
        public GametypeConfiguration[] Gametypes { get; set; }
        public QuickMessageConfiguration[] QuickMessages {get; set;}
        public string[] DisallowedClientNames { get; set; }
        public GameStringConfiguration GameStrings { get; set; }

        public IBaseConfiguration Generate() => this;

        public string Name() => "DefaultConfiguration";
    }
}
