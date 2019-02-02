using SharedLibraryCore.RCon;

namespace SharedLibraryCore.Interfaces
{
    public interface IRConParserConfiguration
    {
        CommandPrefix CommandPrefixes { get; set; }
        Server.Game GameName { get; set; }
        ParserRegex Status { get; set; }
        ParserRegex Dvar { get; set; }
    }
}
