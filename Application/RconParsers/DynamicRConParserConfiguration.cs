using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.RCon;

namespace IW4MAdmin.Application.RconParsers
{
    /// <summary>
    /// generic implementation of the IRConParserConfiguration
    /// allows script plugins to generate dynamic RCon configurations
    /// </summary>
    sealed internal class DynamicRConParserConfiguration : IRConParserConfiguration
    {
        public CommandPrefix CommandPrefixes { get; set; }
        public ParserRegex Status { get; set; } = new ParserRegex();
        public ParserRegex Dvar { get; set; } = new ParserRegex();
        public bool WaitForResponse { get; set; } = true;
    }
}
