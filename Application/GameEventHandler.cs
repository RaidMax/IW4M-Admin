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
        readonly IManager Manager;
        public GameEventHandler(IManager mgr)
        {
            Manager = mgr;
        }

        public void AddEvent(GameEvent gameEvent)
        {
            ((Manager as ApplicationManager).OnServerEvent)(this, new GameEventArgs(null, false, gameEvent));
        }
    }
}
