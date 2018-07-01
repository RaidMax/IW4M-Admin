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
        private Queue<GameEvent> DelayedEventQueue;
        private IManager Manager;
        private const int DelayAmount = 5000;
        private DateTime LastDelayedEvent;

        public GameEventHandler(IManager mgr)
        {
            EventQueue = new ConcurrentQueue<GameEvent>();
            DelayedEventQueue = new Queue<GameEvent>();

            Manager = mgr;
        }

        public void AddEvent(GameEvent gameEvent, bool delayedExecution = false)
        {
#if DEBUG
            Manager.GetLogger().WriteDebug($"Got new event of type {gameEvent.Type} for {gameEvent.Owner}");
#endif
            if (delayedExecution)
            {
                DelayedEventQueue.Enqueue(gameEvent);
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

            if (DelayedEventQueue.Count > 0 &&
               (DateTime.Now - LastDelayedEvent).TotalMilliseconds > DelayAmount)
            {
                LastDelayedEvent = DateTime.Now;
#if DEBUG
                Manager.GetLogger().WriteDebug("Getting next delayed event to be processed");
#endif
                if (!DelayedEventQueue.TryDequeue(out GameEvent newEvent))
                {
                    Manager.GetLogger().WriteWarning("Could not dequeue delayed event for processing");
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
