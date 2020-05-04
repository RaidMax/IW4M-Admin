using SharedLibraryCore;
using SharedLibraryCore.Events;
using SharedLibraryCore.Interfaces;
using System;
using System.Linq;

namespace IW4MAdmin.Application
{
    class SerialGameEventHandler : IEventHandler
    {
        private delegate void GameEventAddedEventHandler(object sender, GameEventArgs args);
        private event GameEventAddedEventHandler GameEventAdded;

        private static readonly GameEvent.EventType[] overrideEvents = new[]
        {
            GameEvent.EventType.Connect,
            GameEvent.EventType.Disconnect,
            GameEvent.EventType.Quit,
            GameEvent.EventType.Stop
        };

        public SerialGameEventHandler()
        {
            GameEventAdded += GameEventHandler_GameEventAdded;
        }

        private async void GameEventHandler_GameEventAdded(object sender, GameEventArgs args)
        {
            await (sender as IManager).ExecuteEvent(args.Event);
            EventApi.OnGameEvent(args.Event);
        }

        public void HandleEvent(IManager manager, GameEvent gameEvent)
        {
            if (manager.IsRunning || overrideEvents.Contains(gameEvent.Type))
            {
                GameEventAdded?.Invoke(manager, new GameEventArgs(null, false, gameEvent));
            }
        }
    }
}
