using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System.Collections.Generic;

namespace ApplicationTests.Mocks
{
    class EventHandlerMock : IEventHandler
    {
        public IList<GameEvent> Events = new List<GameEvent>();
        private readonly bool _autoExecute;

        public EventHandlerMock(bool autoExecute = false)
        {
            _autoExecute = autoExecute;
        }

        public void HandleEvent(IManager manager, GameEvent gameEvent)
        {
            Events.Add(gameEvent);

            if (_autoExecute)
            {
                gameEvent.Owner?.ExecuteEvent(gameEvent);
                gameEvent.Complete();
            }
        }
    }
}
