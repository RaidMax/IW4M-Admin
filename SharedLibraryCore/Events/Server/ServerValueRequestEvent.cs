using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Events.Server;

public class ServerValueRequestEvent : GameServerEvent
{
    public ServerValueRequestEvent(string valueName, IGameServer server)
    {
        ValueName = valueName;
        Server = server;
    }
    
    public string ValueName { get; init; }
    public int? DelayMs { get; init; }
    public int? TimeoutMs { get; init; }
    public string FallbackValue { get; init; }
}
