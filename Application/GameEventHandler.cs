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

        public GameEventHandler(IManager mgr)
        {
            Manager = mgr;
            OutOfOrderEvents = new SortedList<long, GameEvent>();
        }

        public bool AddEvent(GameEvent gameEvent)
        {
#if DEBUG
            Manager.GetLogger().WriteDebug($"Got new event of type {gameEvent.Type} for {gameEvent.Owner} with id {gameEvent.Id}");
#endif
//            GameEvent sortedEvent = null;
//            lock (OutOfOrderEvents)
//            {
//                sortedEvent = OutOfOrderEvents.Values.FirstOrDefault();

//                while (sortedEvent?.Id == Interlocked.Read(ref NextEventId))
//                {
//                    if (OutOfOrderEvents.Count > 0)
//                    {
//                        OutOfOrderEvents.RemoveAt(0);
//                    }

//                    AddEvent(sortedEvent);
//                    sortedEvent = OutOfOrderEvents.Values.FirstOrDefault();
//                }
//            }

//            // both the gameEvent Id and the LastEventId are thread safe because we want to synchronize when the 
//            // event occurs
//            if (gameEvent.Id == Interlocked.Read(ref NextEventId))
//            {
//#if DEBUG == true
//                Manager.GetLogger().WriteDebug($"sent event with id {gameEvent.Id} to be processed");
//#endif
                ((Manager as ApplicationManager).OnServerEvent)(this, new GameEventArgs(null, false, gameEvent));
                return true;
//                Interlocked.Increment(ref NextEventId);
//            }

//            // a "newer" event has been added before and "older" one has been added (due to threads and context switching)
//            // so me must wait until the next expected one arrives     
//            else
//            {
//#if DEBUG == true
//                Manager.GetLogger().WriteWarning("Incoming event is out of order");
//                Manager.GetLogger().WriteDebug($"Expected event Id is {Interlocked.Read(ref NextEventId)}, but got {gameEvent.Id} instead");
//#endif

//                // this prevents multiple threads from adding simultaneously
//                lock (OutOfOrderEvents)
//                {
//                    if (!OutOfOrderEvents.TryGetValue(gameEvent.Id, out GameEvent discard))
//                    {
//                        OutOfOrderEvents.Add(gameEvent.Id, gameEvent);
//                    }
//                }
//            }
//            return true;
        }
    }
}
