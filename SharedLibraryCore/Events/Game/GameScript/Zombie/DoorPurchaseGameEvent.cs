namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

public class DoorPurchaseGameEvent : ClientGameEvent
{
    public int Cost { get; init; }
}
