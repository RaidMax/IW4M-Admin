using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// This class handle games events (from log, manual events, etc)
    /// </summary>
    public interface IEventHandler
    {
        /// <summary>
        /// Add a game event event to the queue to be processed
        /// </summary>
        /// <param name="gameEvent">Game event</param>
        /// <param name="delayedExecution">don't signal that an event has been aded</param>
        void AddEvent(GameEvent gameEvent, bool delayedExecution = false);
        /// <summary>
        /// Get the next event to be processed
        /// </summary>
        /// <returns>Game event that needs to be processed</returns>
        GameEvent GetNextEvent();
        /// <summary>
        /// If an event has output. Like executing a command wait until it's available
        /// </summary>
        /// <returns>List of output strings</returns>
        string[] GetEventOutput();
    }
}
