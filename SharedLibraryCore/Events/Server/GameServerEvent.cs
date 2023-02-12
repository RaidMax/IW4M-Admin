using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Events.Server;

public abstract class GameServerEvent : CoreEvent
{
    public IGameServer Server { get; init; }
}
