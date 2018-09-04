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
        void AddEvent(GameEvent gameEvent);
    }
}
