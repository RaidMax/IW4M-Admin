using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// represents the abtraction of game log reading
    /// </summary>
    public interface IGameLogReader
    {
        /// <summary>
        /// get new events that have occured since the last poll
        /// </summary>
        /// <param name="server"></param>
        /// <param name="fileSizeDiff"></param>
        /// <param name="startPosition"></param>
        /// <returns></returns>
        ICollection<GameEvent> ReadEventsFromLog(Server server, long fileSizeDiff, long startPosition);
        /// <summary>
        /// how long the log file is
        /// </summary>
        long Length { get; }
        /// <summary>
        /// how often to poll the log file
        /// </summary>
        int UpdateInterval { get; }
    }
}
