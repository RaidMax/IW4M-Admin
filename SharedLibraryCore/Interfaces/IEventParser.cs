using System;
using System.Collections.Generic;
using System.Text;

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
    }
}
