namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

public class TrapActivateGameEvent : ClientGameEvent
{
    public string TrapType { get; init; }
    public int Cost { get; init; }
}
