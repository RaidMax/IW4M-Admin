using SharedLibraryCore.RCon;

namespace SharedLibraryCore.Interfaces
{
    public interface IRConParserConfiguration
    {
        /// <summary>
        /// stores the command format for console commands
        /// </summary>
        CommandPrefix CommandPrefixes { get; set; }
        /// <summary>
        /// optionally stores the game name type
        /// </summary>
        Server.Game GameName { get; set; }
        /// <summary>
        /// stores the regex info for parsing get status response
        /// </summary>
        ParserRegex Status { get; set; }
        /// <summary>
        /// stores the regex info for parsing get DVAR responses
        /// </summary>
        ParserRegex Dvar { get; set; }
        /// <summary>
        /// indicates if the application should wait for response from server
        /// when executing a command
        /// </summary>
        bool WaitForResponse { get; set; }
    }
}
