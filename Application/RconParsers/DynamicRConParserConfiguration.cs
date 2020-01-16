using SharedLibraryCore.Interfaces;
using SharedLibraryCore.RCon;
using System.Globalization;

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
        public ParserRegex MapStatus { get; set; } = new ParserRegex();
        public ParserRegex Dvar { get; set; } = new ParserRegex();
        public string ServerNotRunningResponse { get; set; }
        public bool WaitForResponse { get; set; } = true;
        public NumberStyles GuidNumberStyle { get; set; } = NumberStyles.HexNumber;
    }
}
