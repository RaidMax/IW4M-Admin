using SharedLibraryCore.RCon;
using System.Globalization;

namespace SharedLibraryCore.Interfaces
{
    public interface IRConParserConfiguration
    {
        /// <summary>
        /// stores the command format for console commands
        /// </summary>
        CommandPrefix CommandPrefixes { get; set; }

        /// <summary>
        /// stores the regex info for parsing get status response
        /// </summary>
        ParserRegex Status { get; set; }

        /// <summary>
        /// stores regex info for parsing the map line from rcon status response
        /// </summary>
        ParserRegex MapStatus { get; set; }

        /// <summary>
        /// stores the regex info for parsing get DVAR responses
        /// </summary>
        ParserRegex Dvar { get; set; }

        /// <summary>
        /// stores the regex info for parsing the header of a status response
        /// </summary>
        ParserRegex StatusHeader { get; set; }

        /// <summary>
        /// Specifies the expected response message from rcon when the server is not running
        /// </summary>
        string ServerNotRunningResponse { get; set; }

        /// <summary>
        /// indicates if the application should wait for response from server
        /// when executing a command
        /// </summary>
        bool WaitForResponse { get; set; }

        /// <summary>
        /// indicates the format expected for parsed guids
        /// </summary>
        NumberStyles GuidNumberStyle { get; set; }
    }
}
