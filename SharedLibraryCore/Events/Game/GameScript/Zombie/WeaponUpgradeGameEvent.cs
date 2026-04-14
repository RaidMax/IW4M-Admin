namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

public class WeaponUpgradeGameEvent : ClientGameEvent
{
    public string OldWeapon { get; init; }
    public string NewWeapon { get; init; }
    public int Cost { get; init; }
}
