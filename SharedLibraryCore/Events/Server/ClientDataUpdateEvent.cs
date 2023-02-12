using System.Collections.Generic;
using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore.Events.Server;

public class ClientDataUpdateEvent : GameServerEvent
{
    public IReadOnlyCollection<EFClient> Clients { get; init; }
}
