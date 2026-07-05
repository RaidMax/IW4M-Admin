namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

public class BoxUseGameEvent : ClientGameEvent
{
    public enum BoxOutcome
    {
        Take,
        Pass,
        Teddy
    }

    public BoxOutcome Outcome { get; init; }
    public string? WeaponName { get; init; }
    public int Cost { get; init; }
}
