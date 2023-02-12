namespace  SharedLibraryCore.Events.Server;

public class ServerCommandExecuteEvent : GameServerEvent
{
    public string Command { get; init; }
    public string[] Output { get; init; }
}
