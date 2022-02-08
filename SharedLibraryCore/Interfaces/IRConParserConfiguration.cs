using System.Collections.Generic;
using System.Globalization;
using SharedLibraryCore.Formatting;
using SharedLibraryCore.RCon;

namespace SharedLibraryCore.Interfaces
{
    public interface IRConParserConfiguration
    {
        /// <summary>
        ///     stores the command format for console commands
        /// </summary>
        CommandPrefix CommandPrefixes { get; }

        /// <summary>
        ///     stores the regex info for parsing get status response
        /// </summary>
        ParserRegex Status { get; }

        /// <summary>
        ///     stores regex info for parsing the map line from rcon status response
        /// </summary>
        ParserRegex MapStatus { get; }

        /// <summary>
        ///     stores regex info for parsing the gametype line from rcon status response
        /// </summary>
        ParserRegex GametypeStatus { get; }

        /// <summary>
        ///     stores regex info for parsing hostname line from rcon status response
        /// </summary>
        ParserRegex HostnameStatus { get; }

        /// <summary>
        ///     stores regex info for parsing max players line from rcon status response
        /// </summary>
        ParserRegex MaxPlayersStatus { get; }

        /// <summary>
        ///     stores the regex info for parsing get DVAR responses
        /// </summary>
        ParserRegex Dvar { get; }

        /// <summary>
        ///     stores the regex info for parsing the header of a status response
        /// </summary>
        ParserRegex StatusHeader { get; }

        /// <summary>
        ///     Specifies the expected response message from rcon when the server is not running
        /// </summary>
        string ServerNotRunningResponse { get; }

        /// <summary>
        ///     indicates if the application should wait for response from server
        ///     when executing a command
        /// </summary>
        bool WaitForResponse { get; }

        /// <summary>
        ///     indicates the format expected for parsed guids
        /// </summary>
        NumberStyles GuidNumberStyle { get; }

        /// <summary>
        ///     specifies simple mappings for dvar names in scenarios where the needed
        ///     information is not stored in a traditional dvar name
        /// </summary>
        IDictionary<string, string> OverrideDvarNameMapping { get; }

        /// <summary>
        ///     specifies the default dvar values for games that don't support certain dvars
        /// </summary>
        IDictionary<string, string> DefaultDvarValues { get; }

        /// <summary>
        ///     specifies how many lines can be used for ingame notice
        /// </summary>
        int NoticeMaximumLines { get; set; }

        /// <summary>
        ///     specifies how many characters can be displayed per notice line
        /// </summary>
        int NoticeMaxCharactersPerLine { get; }

        /// <summary>
        ///     specifies the characters used to split a line
        /// </summary>
        string NoticeLineSeparator { get; }

        /// <summary>
        ///     Default port the game listens to RCon requests on
        /// </summary>
        int? DefaultRConPort { get; }

        /// <summary>
        ///     Default Indicator of where the game is installed (ex file path or registry entry)
        /// </summary>
        string DefaultInstallationDirectoryHint { get; }

        ColorCodeMapping ColorCodeMapping { get; }
        
        short FloodProtectInterval { get; }
    }
}
