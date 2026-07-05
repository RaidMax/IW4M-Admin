namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

public class WeaponPurchaseGameEvent : ClientGameEvent
{
    public string WeaponName { get; init; }
    public int Cost { get; init; }
}
