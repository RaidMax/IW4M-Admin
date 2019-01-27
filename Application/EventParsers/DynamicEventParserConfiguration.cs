using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.EventParsers
{
    class DynamicEventParserConfiguration : IEventParserConfiguration
    {
        public string GameDirectory { get; set; }
        public string SayRegex { get; set; }
        public string JoinRegex { get; set; }
        public string QuitRegex { get; set; }
        public string KillRegex { get; set; }
        public string DamageRegex { get; set; }
    }
}
