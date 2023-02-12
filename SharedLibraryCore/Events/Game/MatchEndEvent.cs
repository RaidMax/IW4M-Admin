namespace SharedLibraryCore.Events.Game;

public class MatchEndEvent : GameEventV2
{
    public string SessionData { get; init; }
}
