namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

public class PlayerStatUpdatedGameEvent : ClientGameEvent
{
    public enum StatUpdateType
    {
        Absolute,
        Increment,
        Decrement
    }
    
    public string StatTag { get; set; }
    public int StatValue { get; set; }
    public StatUpdateType UpdateType { get; set; }
}
