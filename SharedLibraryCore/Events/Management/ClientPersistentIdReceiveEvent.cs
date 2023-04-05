using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore.Events.Management;

public class ClientPersistentIdReceiveEvent : ClientStateEvent
{
    public ClientPersistentIdReceiveEvent(EFClient client, string persistentId)
    {
        Client = client;
        PersistentId = persistentId;
    }
    
    public string PersistentId { get; init; }
}
