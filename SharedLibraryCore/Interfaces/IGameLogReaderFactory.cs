using System;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     factory interface to create game log readers based on the log file uri
    /// </summary>
    public interface IGameLogReaderFactory
    {
        /// <summary>
        ///     generates a new game log reader based on the provided Uri
        /// </summary>
        /// <param name="logUris">collection of log uri used to generate the log reader</param>
        /// <param name="eventParser">event parser for the log reader</param>
        /// <returns></returns>
        IGameLogReader CreateGameLogReader(Uri[] logUris, IEventParser eventParser);
    }
}