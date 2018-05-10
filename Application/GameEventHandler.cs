using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace IW4MAdmin.Application
{
    class GameEventHandler : IEventHandler
    {
        private ConcurrentQueue<GameEvent> EventQueue;
        private Queue<GameEvent> StatusSensitiveQueue;
        private IManager Manager;

        public GameEventHandler(IManager mgr)
        {
            EventQueue = new ConcurrentQueue<GameEvent>();
            StatusSensitiveQueue = new Queue<GameEvent>();

            Manager = mgr;
        }

        public void AddEvent(GameEvent gameEvent)
        {
#if DEBUG
            Manager.GetLogger().WriteDebug($"Got new event of type {gameEvent.Type} for {gameEvent.Owner}");
#endif
            // we need this to keep accurate track of the score
            if (gameEvent.Type == GameEvent.EventType.Kill ||
                gameEvent.Type == GameEvent.EventType.Damage ||
                gameEvent.Type == GameEvent.EventType.ScriptDamage ||
                gameEvent.Type == GameEvent.EventType.ScriptKill ||
                gameEvent.Type == GameEvent.EventType.MapChange)
            {
#if DEBUG
                Manager.GetLogger().WriteDebug($"Added sensitive event to queue");
#endif
                lock (StatusSensitiveQueue)
                {
                    StatusSensitiveQueue.Enqueue(gameEvent);
                }
                return;
            }

            else
            {
                EventQueue.Enqueue(gameEvent);
                Manager.SetHasEvent();
            }
#if DEBUG
            Manager.GetLogger().WriteDebug($"There are now {EventQueue.Count} events in queue");
#endif
        }

        public string[] GetEventOutput()
        {
            throw new NotImplementedException();
        }

        public GameEvent GetNextSensitiveEvent()
        {
            if (StatusSensitiveQueue.Count > 0)
            {
                lock (StatusSensitiveQueue)
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
