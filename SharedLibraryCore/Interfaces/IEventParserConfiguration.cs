using System.Collections.Generic;
using System.Globalization;
using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore.Interfaces
{
    public interface IEventParserConfiguration
    {
        /// <summary>
        ///     stores the fs_game directory (this folder may vary between different clients)
        /// </summary>
        string GameDirectory { get; set; }

        /// <summary>
        ///     stores the regex information for a say event printed in the game log
        /// </summary>
        ParserRegex Say { get; set; }

        /// <summary>
        ///     stores the special ASCII value used by CoD games that prevents the text in the chat from being localized
        /// </summary>
        string LocalizeText { get; set; }

        /// <summary>
        ///     stores the regex information for a join event printed in the game log
        /// </summary>
        ParserRegex Join { get; set; }
        
        /// <summary>
        ///     stores the regex information for a join team event printed in the game log
        /// </summary>
        ParserRegex JoinTeam { get; set; }

        /// <summary>
        ///     stores the regex information for a quit event printed in the game log
        /// </summary>
        ParserRegex Quit { get; set; }

        /// <summary>
        ///     stores the regex information for a kill event printed in the game log
        /// </summary>
        ParserRegex Kill { get; set; }

        /// <summary>
        ///     stores the regex information for a damage event printed in the game log
        /// </summary>
        ParserRegex Damage { get; set; }

        /// <summary>
        ///     stores the regex information for an action event printed in the game log
        /// </summary>
        ParserRegex Action { get; set; }

        /// <summary>
        ///     stores the regex information for the time prefix in game log
        /// </summary>
        ParserRegex Time { get; set; }

        /// <summary>
        ///     stores the regex information for the map change game log
        /// </summary>
        ParserRegex MapChange { get; }

        /// <summary>
        ///     stores the regex information for the map end game log
        /// </summary>
        ParserRegex MapEnd { get; }

        /// <summary>
        ///     indicates the format expected for parsed guids
        /// </summary>
        NumberStyles GuidNumberStyle { get; set; }
        
        /// <summary>
        /// maps the team code name to a type type eg "CT" -> Allies
        /// </summary>
        Dictionary<string, EFClient.TeamType> TeamMapping { get; }
    }
}
