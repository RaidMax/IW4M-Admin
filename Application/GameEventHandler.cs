using SharedLibraryCore;
using SharedLibraryCore.Events;
using SharedLibraryCore.Interfaces;
using System.Threading;

namespace IW4MAdmin.Application
{
    class GameEventHandler : IEventHandler
    {
        readonly ApplicationManager Manager;
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
            Manager.OnServerEvent?.Invoke(gameEvent.Owner, new GameEventArgs(null, false, gameEvent));
        }
    }
}
