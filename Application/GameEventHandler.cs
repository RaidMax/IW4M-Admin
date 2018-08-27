using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace IW4MAdmin.Application
{
    class GameEventHandler : IEventHandler
    {
        private readonly IManager Manager;

        public GameEventHandler(IManager mgr)
        {
            Manager = mgr;
        }

        public void AddEvent(GameEvent gameEvent)
        {
#if DEBUG
            Manager.GetLogger().WriteDebug($"Got new event of type {gameEvent.Type} for {gameEvent.Owner}");
#endif
            // todo: later
            ((Manager as ApplicationManager).OnServerEvent)(this, new ApplicationManager.GameEventArgs(null, false, gameEvent));
        }
    }
}
