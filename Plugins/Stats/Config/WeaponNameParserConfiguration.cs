using SharedLibraryCore;

namespace Stats.Config
{
    public class WeaponNameParserConfiguration
    {
        public Server.Game Game { get; set; }
        public char[] Delimiters { get; set; }
        public string WeaponSuffix { get; set; }
    }
}