using Data.Models.Client;

namespace SharedLibraryCore.Events.Management;

public class ClientPermissionChangeEvent : ClientStateEvent
{
    public EFClient.Permission OldPermission { get; init; }
    public EFClient.Permission NewPermission { get; init; }
}
