using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Events.Server;

public class ServerStatusReceiveEvent : GameServerEvent
{ 
    public IStatusResponse Response { get; set; }
}
