using SharedLibraryCore;
using SharedLibraryCore.Events;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IW4MAdmin.Application
{
    class GameEventHandler : IEventHandler
    {
        private readonly IManager Manager;
        static long NextEventId = 1;
        private readonly SortedList<long, GameEvent> OutOfOrderEvents;
        private readonly SemaphoreSlim ProcessingEvent;

        public GameEventHandler(IManager mgr)
        {
            Manager = mgr;
            OutOfOrderEvents = new SortedList<long, GameEvent>();
            ProcessingEvent = new SemaphoreSlim(0);
            ProcessingEvent.Release();
        }

        public bool AddEvent(GameEvent gameEvent)
        {
#if DEBUG
            Manager.GetLogger().WriteDebug($"Got new event of type {gameEvent.Type} for {gameEvent.Owner} with id {gameEvent.Id}");
#endif
            while (OutOfOrderEvents.Values.FirstOrDefault()?.Id == Interlocked.Read(ref NextEventId))
            {
                lock (OutOfOrderEvents)
                {
                    var fixedEvent = OutOfOrderEvents.Values[0];
                    OutOfOrderEvents.RemoveAt(0);
                    AddEvent(fixedEvent);
                }
            }

            // both the gameEvent Id and the LastEventId are thread safe because we want to synchronize when the 
            // event occurs
            if (gameEvent.Id == Interlocked.Read(ref NextEventId))
            {
                Manager.GetLogger().WriteDebug($"Starting to wait for event with id {gameEvent.Id}");
                ((Manager as ApplicationManager).OnServerEvent)(this, new GameEventArgs(null, false, gameEvent));
                Manager.GetLogger().WriteDebug($"Finished waiting for event with id {gameEvent.Id}");
                Interlocked.Increment(ref NextEventId);
            }

            // a "newer" event has been added before and "older" one has been added (due to threads and context switching)
            // so me must wait until the next expected one arrives     
            else
            {
                Manager.GetLogger().WriteWarning("Incoming event is out of order");
                Manager.GetLogger().WriteDebug($"Expected event Id is {Interlocked.Read(ref NextEventId)}, but got {gameEvent.Id} instead");

                // this prevents multiple threads from adding simultaneously
                lock (OutOfOrderEvents)
                {
                    if (!OutOfOrderEvents.TryGetValue(gameEvent.Id, out GameEvent discard))
                    {
                        OutOfOrderEvents.Add(gameEvent.Id, gameEvent);
                    }
                }
            }
            return true;
        }
    }
}
