using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin.Application
{
    class GameEventHandler : IEventHandler
    {
        private ConcurrentQueue<GameEvent> EventQueue;
        private ConcurrentQueue<GameEvent> StatusSensitiveQueue;
        private IManager Manager;

        public GameEventHandler(IManager mgr)
        {
            EventQueue = new ConcurrentQueue<GameEvent>();
            StatusSensitiveQueue = new ConcurrentQueue<GameEvent>();
            Manager = mgr;
        }

        public void AddEvent(GameEvent gameEvent)
        {
#if DEBUG
            Manager.GetLogger().WriteDebug($"Got new event of type {gameEvent.Type} for {gameEvent.Owner}");
#endif
            // we need this to keep accurate track of the score
            if (gameEvent.Type == GameEvent.EventType.Script ||
                gameEvent.Type == GameEvent.EventType.Kill ||
                gameEvent.Type == GameEvent.EventType.MapChange)
            {
#if DEBUG
                Manager.GetLogger().WriteDebug($"Added sensitive event to queue");
#endif
                StatusSensitiveQueue.Enqueue(gameEvent);
                return;
            }

            else
            {
                EventQueue.Enqueue(gameEvent);
            }
#if DEBUG
            Manager.GetLogger().WriteDebug($"There are now {EventQueue.Count} events in queue");
#endif
            Manager.SetHasEvent();
        }

        public string[] GetEventOutput()
        {
            throw new NotImplementedException();
        }

        public GameEvent GetNextSensitiveEvent()
        {
            if (StatusSensitiveQueue.Count > 0)
            {
                if (!StatusSensitiveQueue.TryDequeue(out GameEvent newEvent))
                {
                    Manager.GetLogger().WriteWarning("Could not dequeue time sensitive event for processing");
                }

                else
                {
                    return newEvent;
                }
            }

            return null;
        }

        public GameEvent GetNextEvent()
        {
            if (EventQueue.Count > 0)
            {
#if DEBUG
                Manager.GetLogger().WriteDebug("Getting next event to be processed");
#endif
                if (!EventQueue.TryDequeue(out GameEvent newEvent))
                {
                    Manager.GetLogger().WriteWarning("Could not dequeue event for processing");
                }

                else
                {
                    return newEvent;
                }
            }

            return null;
        }
    }
}
