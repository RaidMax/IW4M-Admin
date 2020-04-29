using IW4MAdmin.Application.Misc;
using SharedLibraryCore;
using SharedLibraryCore.Events;
using SharedLibraryCore.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IW4MAdmin.Application
{
    class GameEventHandler : IEventHandler
    {
        private const int MAX_CONCURRENT_EVENTS = 10;
        private readonly ApplicationManager _manager;
        private readonly SemaphoreSlim _processingEvents;
        private static readonly GameEvent.EventType[] overrideEvents = new[]
        {
            GameEvent.EventType.Connect,
            GameEvent.EventType.Disconnect,
            GameEvent.EventType.Quit,
            GameEvent.EventType.Stop
        };

        public GameEventHandler(IManager mgr)
        {
            _manager = (ApplicationManager)mgr;
            _processingEvents = new SemaphoreSlim(MAX_CONCURRENT_EVENTS, MAX_CONCURRENT_EVENTS);
        }

        private Task GameEventHandler_GameEventAdded(object sender, GameEventArgs args)
        {
            try
            {
                // this is not elegant and there's probably a much better way to do it, but it works for now
                _processingEvents.Wait();
                EventApi.OnGameEvent(sender, args);
                return _manager.ExecuteEvent(args.Event);
            }

            catch
            {

            }

            finally
            {
                if (_processingEvents.CurrentCount < MAX_CONCURRENT_EVENTS)
                {
                    _processingEvents.Release();
                }
            }

            return Task.CompletedTask;
        }

        public void AddEvent(GameEvent gameEvent)
        {
#if DEBUG
            ThreadPool.GetMaxThreads(out int workerThreads, out int n);
            ThreadPool.GetAvailableThreads(out int availableThreads, out int m);
            gameEvent.Owner.Logger.WriteDebug($"There are {workerThreads - availableThreads} active threading tasks");

#endif
            if (_manager.Running || overrideEvents.Contains(gameEvent.Type))
            {
#if DEBUG
                gameEvent.Owner.Logger.WriteDebug($"Adding event with id {gameEvent.Id}");
#endif
                Task.Run(() => GameEventHandler_GameEventAdded(this, new GameEventArgs(null, false, gameEvent)));
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
