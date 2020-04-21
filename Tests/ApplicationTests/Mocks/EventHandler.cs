using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System.Collections.Generic;

namespace ApplicationTests.Mocks
{
    class MockEventHandler : IEventHandler
    {
        public IList<GameEvent> Events = new List<GameEvent>();

        public void AddEvent(GameEvent gameEvent)
        {
            Events.Add(gameEvent);
        }
    }
}
