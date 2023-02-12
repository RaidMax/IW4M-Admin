using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore.Events.Game;

public abstract class ClientGameEvent : GameEventV2
{
    public string ClientName { get; init; }
    public string ClientNetworkId { get; init; }
    public int ClientSlotNumber { get; init; }

    public EFClient Client => Origin;
}
