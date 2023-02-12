namespace SharedLibraryCore.Events.Game;

public class ClientMessageEvent : ClientGameEvent
{
    public bool IsTeamMessage { get; init; }
}
