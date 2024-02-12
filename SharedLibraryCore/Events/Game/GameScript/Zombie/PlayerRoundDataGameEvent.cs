namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

public class PlayerRoundDataGameEvent : ClientGameEvent
{
    public int TotalScore { get; init; }
    public int CurrentScore { get; init; }
    public int CurrentRound { get; init; }
    public bool IsGameOver { get; init; }
}
