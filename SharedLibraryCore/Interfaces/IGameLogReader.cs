using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     represents the abstraction of game log reading
    /// </summary>
    public interface IGameLogReader
    {
        /// <summary>
        ///     how long the log file is
        /// </summary>
        long Length { get; }

        /// <summary>
        ///     how often to poll the log file
        /// </summary>
        int UpdateInterval { get; }

        /// <summary>
        ///     get new events that have occured since the last poll
        /// </summary>
        /// <param name="fileSizeDiff"></param>
        /// <param name="startPosition"></param>
        /// <param name="server"></param>
        /// <returns></returns>
        Task<IEnumerable<GameEvent>> ReadEventsFromLog(long fileSizeDiff, long startPosition, Server server);
    }
}
