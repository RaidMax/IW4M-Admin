using SharedLibraryCore;
using SharedLibraryCore.Events;
using SharedLibraryCore.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace IW4MAdmin.Application
{
    class GameEventHandler : IEventHandler
    {
        static long NextEventId = 1;
        readonly IManager Manager;
        readonly SortedList<long, GameEvent> OutOfOrderEvents;
        readonly SemaphoreSlim IsProcessingEvent;

        public GameEventHandler(IManager mgr)
        {
            Manager = mgr;
            OutOfOrderEvents = new SortedList<long, GameEvent>();
            IsProcessingEvent = new SemaphoreSlim(0);
            IsProcessingEvent.Release();
        }

        public void AddEvent(GameEvent gameEvent)
        {
#if DEBUG
            Manager.GetLogger().WriteDebug($"Got new event of type {gameEvent.Type} for {gameEvent.Owner} with id {gameEvent.Id}");
#endif
            GameEvent sortedEvent = null;
            lock (OutOfOrderEvents)
            {
                sortedEvent = OutOfOrderEvents.Values.FirstOrDefault();

                while (sortedEvent?.Id == Interlocked.Read(ref NextEventId))
                {
                    if (OutOfOrderEvents.Count > 0)
                    {
                        OutOfOrderEvents.RemoveAt(0);
                    }

                    AddEvent(sortedEvent);
                    sortedEvent = OutOfOrderEvents.Values.FirstOrDefault();
                }
            }

            // both the gameEvent Id and the LastEventId are thread safe because we want to synchronize when the 
            // event occurs
            if (gameEvent.Id == Interlocked.Read(ref NextEventId))
            {
//#if DEBUG == true
//                Manager.GetLogger().WriteDebug($"sent event with id {gameEvent.Id} to be processed");
//                IsProcessingEvent.Wait();
//#else
//                if (GameEvent.IsEventTimeSensitive(gameEvent) &&
//                    !IsProcessingEvent.Wait(30 * 1000))
//                {
//                    Manager.GetLogger().WriteError($"{Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_COMMAND_TIMEOUT"]} [{gameEvent.Id}, {gameEvent.Type}]");
//                }
//#endif
                ((Manager as ApplicationManager).OnServerEvent)(this, new GameEventArgs(null, false, gameEvent));

                //if (GameEvent.IsEventTimeSensitive(gameEvent))
                //{
                //   if( !gameEvent.OnProcessed.Wait(30 * 1000))
                //    {
                //        Manager.GetLogger().WriteError($"{Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_EVENT_TIMEOUT"]} [{gameEvent.Id}, {gameEvent.Type}]");
                //    }
                //}
                Interlocked.Increment(ref NextEventId);
                //#if DEBUG == true
                //                gameEvent.OnProcessed.Wait();
                //#else
                //                if (GameEvent.IsEventTimeSensitive(gameEvent) &&
                //                    !gameEvent.OnProcessed.Wait(30 * 1000))
                //                {
                //                    Manager.GetLogger().WriteError($"{Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_EVENT_TIMEOUT"]} [{gameEvent.Id}, {gameEvent.Type}]");
                //                }
                //#endif
                //                Interlocked.Increment(ref NextEventId);
                //                if (GameEvent.IsEventTimeSensitive(gameEvent))
                //                {
                //                    IsProcessingEvent.Release();
                //                }
            }

            // a "newer" event has been added before and "older" one has been added (due to threads and context switching)
            // so me must wait until the next expected one arrives     
            else
            {
#if DEBUG == true
                Manager.GetLogger().WriteWarning("Incoming event is out of order");
                Manager.GetLogger().WriteDebug($"Expected event Id is {Interlocked.Read(ref NextEventId)}, but got {gameEvent.Id} instead");
#endif

                // this prevents multiple threads from adding simultaneously
                lock (OutOfOrderEvents)
                {
                    if (!OutOfOrderEvents.TryGetValue(gameEvent.Id, out GameEvent discard))
                    {
                        OutOfOrderEvents.Add(gameEvent.Id, gameEvent);
                    }
                }
            }
        }
    }
}
