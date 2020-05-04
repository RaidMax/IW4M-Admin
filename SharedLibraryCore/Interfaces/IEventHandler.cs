namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// handles games events (from log, manual events, etc)
    /// </summary>
    public interface IEventHandler
    {
        /// <summary>
        /// Add a game event event to the queue to be processed
        /// </summary>
        /// <param name="manager">application manager instance</param>
        /// <param name="gameEvent">game event</param>
        void HandleEvent(IManager manager, GameEvent gameEvent);
    }
}
