namespace SharedLibraryCore.Events.Server;

public class ServerValueReceiveEvent : GameServerEvent
{
    public Dvar<string> Response { get; init; }
    public bool Success { get; init; }
}
