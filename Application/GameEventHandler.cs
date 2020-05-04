using IW4MAdmin.Application.Misc;
using Newtonsoft.Json;
using SharedLibraryCore;
using SharedLibraryCore.Events;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IW4MAdmin.Application
{
    public class GameEventHandler : IEventHandler
    {
        private readonly EventLog _eventLog;
        private static readonly GameEvent.EventType[] overrideEvents = new[]
        {
            GameEvent.EventType.Connect,
            GameEvent.EventType.Disconnect,
            GameEvent.EventType.Quit,
            GameEvent.EventType.Stop
        };

        public GameEventHandler()
        {
            _eventLog = new EventLog();
        }

        public void HandleEvent(IManager manager, GameEvent gameEvent)
        {
#if DEBUG
            ThreadPool.GetMaxThreads(out int workerThreads, out int n);
            ThreadPool.GetAvailableThreads(out int availableThreads, out int m);
            gameEvent.Owner.Logger.WriteDebug($"There are {workerThreads - availableThreads} active threading tasks");

#endif
            if (manager.IsRunning || overrideEvents.Contains(gameEvent.Type))
            {
#if DEBUG
                gameEvent.Owner.Logger.WriteDebug($"Adding event with id {gameEvent.Id}");
#endif

                EventApi.OnGameEvent(gameEvent);
                Task.Factory.StartNew(() => manager.ExecuteEvent(gameEvent));

                /*if (!_eventLog.ContainsKey(gameEvent.Owner.EndPoint))
                {
                    _eventLog.Add(gameEvent.Owner.EndPoint,new List<GameEvent>());
                }
                _eventLog[gameEvent.Owner.EndPoint].Add(gameEvent);
                string serializedEvents = JsonConvert.SerializeObject(_eventLog, EventLog.BuildVcrSerializationSettings());
                System.IO.File.WriteAllText("output.json", serializedEvents);*/
                //Task.Run(() => GameEventHandler_GameEventAdded(this, new GameEventArgs(null, false, gameEvent)));
            }
#if DEBUG
            else
            {
                gameEvent.Owner.Logger.WriteDebug($"Skipping event as we're shutting down {gameEvent.Id}");
            }
#endif
        }
    }
}
