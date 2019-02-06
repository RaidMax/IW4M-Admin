using static SharedLibraryCore.Server;

namespace SharedLibraryCore.Interfaces
{
    public interface IEventParser
    {
        /// <summary>
        /// Generates a game event based on log line input
        /// </summary>
        /// <param name="server">server the event occurred on</param>
        /// <param name="logLine">single log line string</param>
        /// <returns></returns>
        /// todo: make this integrate without needing the server
        GameEvent GetEvent(Server server, string logLine);
        /// <summary>
        /// Get game specific folder prefix for log files
        /// </summary>
        /// <returns>Game directory prefix</returns>
        IEventParserConfiguration Configuration { get; set; }

        /// <summary>
        /// stores the game/client specific version (usually the value of the "version" DVAR)
        /// </summary>
        string Version { get; set; }

        /// <summary>
        /// specifies the game name (usually the internal studio iteration ie: IW4, T5 etc...)
        /// </summary>
        Game GameName { get; set; }
    }
}
