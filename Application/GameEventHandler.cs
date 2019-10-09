using SharedLibraryCore;
using SharedLibraryCore.Events;
using SharedLibraryCore.Interfaces;
using System.Linq;
using System.Threading;

namespace IW4MAdmin.Application
{
    class GameEventHandler : IEventHandler
    {
        readonly ApplicationManager Manager;
        private static GameEvent.EventType[] overrideEvents = new[]
        {
            GameEvent.EventType.Connect,
            GameEvent.EventType.Disconnect,
            GameEvent.EventType.Quit,
            GameEvent.EventType.Stop
        };

        public GameEventHandler(IManager mgr)
        {
            Manager = (ApplicationManager)mgr;
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
                Manager.OnServerEvent?.Invoke(gameEvent.Owner, new GameEventArgs(null, false, gameEvent));
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
