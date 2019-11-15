using IW4MAdmin.Application.Misc;
using SharedLibraryCore;
using SharedLibraryCore.Events;
using SharedLibraryCore.Interfaces;
using System;
using System.Linq;
using System.Threading;

namespace IW4MAdmin.Application
{
    class GameEventHandler : IEventHandler
    {
        readonly ApplicationManager Manager;
        private readonly EventProfiler _profiler;
        private delegate void GameEventAddedEventHandler(object sender, GameEventArgs args);
        private event GameEventAddedEventHandler GameEventAdded;

        private static readonly GameEvent.EventType[] overrideEvents = new[]
        {
            GameEvent.EventType.Connect,
            GameEvent.EventType.Disconnect,
            GameEvent.EventType.Quit,
            GameEvent.EventType.Stop
        };

        public GameEventHandler(IManager mgr)
        {
            Manager = (ApplicationManager)mgr;
            _profiler = new EventProfiler(mgr.GetLogger(0));
            GameEventAdded += GameEventHandler_GameEventAdded;
        }

        private async void GameEventHandler_GameEventAdded(object sender, GameEventArgs args)
        {
            var start = DateTime.Now;
            await Manager.ExecuteEvent(args.Event);
#if DEBUG
            _profiler.Profile(start, DateTime.Now, args.Event);
#endif
        }

        public void AddEvent(GameEvent gameEvent)
        {
#if DEBUG
            ThreadPool.GetMaxThreads(out int workerThreads, out int n);
            ThreadPool.GetAvailableThreads(out int availableThreads, out int m);
            gameEvent.Owner.Logger.WriteDebug($"There are {workerThreads - availableThreads} active threading tasks");

#endif
            if (Manager.Running || overrideEvents.Contains(gameEvent.Type))
            {
#if DEBUG
                gameEvent.Owner.Logger.WriteDebug($"Adding event with id {gameEvent.Id}");
#endif
                GameEventAdded?.Invoke(this, new GameEventArgs(null, false, gameEvent));
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
