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
        readonly ApplicationManager Manager;
        public GameEventHandler(IManager mgr)
        {
            Manager = (ApplicationManager)mgr;
        }

        public void AddEvent(GameEvent gameEvent)
        {
            Manager.OnServerEvent?.Invoke(gameEvent.Owner, new GameEventArgs(null, false, gameEvent));
        }
    }
}
