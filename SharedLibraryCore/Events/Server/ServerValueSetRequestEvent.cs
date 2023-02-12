using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Events.Server;

public class ServerValueSetRequestEvent : GameServerEvent
{
    public ServerValueSetRequestEvent(string valueName, string value, IGameServer server)
    {
        ValueName = valueName;
        Server = server;
        Value = value;
    }
    
    public string ValueName { get; init; }
    public string Value { get; init; }
    public int? DelayMs { get; init; }
    public int? TimeoutMs { get; init; }
}
