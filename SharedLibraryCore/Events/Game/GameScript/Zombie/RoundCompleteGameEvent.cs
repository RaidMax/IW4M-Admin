namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

public class RoundCompleteGameEvent : GameEventV2
{
    public int RoundNumber { get; init; }
}
