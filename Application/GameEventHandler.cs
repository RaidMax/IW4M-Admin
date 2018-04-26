using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin.Application
{
    class GameEventHandler : IEventHandler
    {
        private Queue<GameEvent> EventQueue;
        private IManager Manager;

        public GameEventHandler(IManager mgr)
        {
            EventQueue = new Queue<GameEvent>();
            Manager = mgr;
        }

        public void AddEvent(GameEvent gameEvent)
        {
#if DEBUG
            Manager.GetLogger().WriteDebug($"Got new event of type {gameEvent.Type} for {gameEvent.Owner}");
#endif
            EventQueue.Enqueue(gameEvent);
#if DEBUG
            Manager.GetLogger().WriteDebug($"There are now {EventQueue.Count} events in queue");
#endif
            Manager.SetHasEvent();
        }

        public string[] GetEventOutput()
        {
            throw new NotImplementedException();
        }

        public GameEvent GetNextEvent()
        {
#if DEBUG
            Manager.GetLogger().WriteDebug("Getting next event to be processed");
#endif

            return EventQueue.Count > 0 ? EventQueue.Dequeue() : null;
        }
    }
}
