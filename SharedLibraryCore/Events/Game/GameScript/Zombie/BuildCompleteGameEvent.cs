namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

public class BuildCompleteGameEvent : ClientGameEvent
{
    public string BuildableName { get; init; }
}
