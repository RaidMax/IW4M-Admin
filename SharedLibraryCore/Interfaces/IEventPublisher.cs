using System;

namespace SharedLibraryCore.Interfaces
{
    public interface IEventPublisher
    {
        event EventHandler<GameEvent> OnClientDisconnect;
        event EventHandler<GameEvent> OnClientConnect;

        void Publish(GameEvent gameEvent);
    }
}