using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Events.Game;

public abstract class GameEventV2 : GameEvent
{
    public IGameServer Server => Owner;
}
