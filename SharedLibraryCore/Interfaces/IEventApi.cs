using SharedLibraryCore.Dtos;
using System.Collections.Generic;

namespace SharedLibraryCore.Interfaces
{
    public interface IEventApi
    {
        /// <summary>
        /// Processes event from server as event info
        /// </summary>
        /// <param name="sender">Object state from Delegate method call</param>
        /// <param name="E">Event to process</param>
        void OnServerEvent(object sender, GameEvent E);
        /// <summary>
        /// Get list of recent events
        /// </summary>
        /// <param name="shouldConsume">specify wether the request should clear all events after retrieving</param>
        /// <returns>List of recent event</returns>
        IEnumerable<EventInfo> GetEvents(bool shouldConsume);
    }
}
