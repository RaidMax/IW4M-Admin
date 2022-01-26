using System;
using System.ComponentModel;

namespace SharedLibraryCore.Events
{
    /// <summary>
    ///     represents the state of a game event for event processing
    /// </summary>
    public class GameEventArgs : AsyncCompletedEventArgs
    {
        public GameEventArgs(Exception error, bool cancelled, GameEvent userState) : base(error, cancelled, userState)
        {
            Event = userState;
        }

        /// <summary>
        ///     Game event that occured on a server
        /// </summary>
        public GameEvent Event { get; }
    }
}