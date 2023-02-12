namespace SharedLibraryCore.Events.Server;

public class ServerValueSetCompleteEvent : GameServerEvent
{
    public bool Success { get; init; }
    public string ValueName { get; init; }
    public string Value { get; set; }
}
