using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Events.Server;

public class ServerCommandRequestExecuteEvent : GameServerEvent
{
    public ServerCommandRequestExecuteEvent(string command, IGameServer server)
    {
        Command = command;
        Server = server;
    }

    public string Command { get; init; }
    public int? DelayMs { get; init; }
    public int? TimeoutMs { get; init; }
}
