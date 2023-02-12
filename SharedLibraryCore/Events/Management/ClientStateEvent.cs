using EFClient = SharedLibraryCore.Database.Models.EFClient;

namespace SharedLibraryCore.Events.Management;

public abstract class ClientStateEvent : ManagementEvent
{
    public EFClient Client { get; init; }
}
