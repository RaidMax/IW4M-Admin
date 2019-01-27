using SharedLibraryCore.RCon;

namespace SharedLibraryCore.Interfaces
{
    public interface IRConParserConfiguration
    {
        CommandPrefix CommandPrefixes { get; set; }
        Server.Game GameName { get; set; }
        string StatusRegex { get; set; }
    }
}
