using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.RCon;

namespace IW4MAdmin.Application.RconParsers
{
    class DynamicRConParserConfiguration : IRConParserConfiguration
    {
        public CommandPrefix CommandPrefixes { get; set; }
        public Server.Game GameName { get; set; }
        public ParserRegex Status { get; set; } = new ParserRegex();
        public ParserRegex Dvar { get; set; } = new ParserRegex();
    }
}
