using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.RCon;

namespace IW4MAdmin.Application.RconParsers
{
    class DynamicRConParserConfiguration : IRConParserConfiguration
    {
        public CommandPrefix CommandPrefixes { get; set; }
        public Server.Game GameName { get; set; }
        public string StatusRegex { get; set; }
    }
}
