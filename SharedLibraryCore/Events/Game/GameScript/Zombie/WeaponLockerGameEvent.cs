namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

// T6 Tranzit / Die Rise / Buried weapon-locker transaction. Player swaps their
// currently-held weapon into the locker (store) or pulls a previously-stored
// weapon back out (retrieve). WeaponName is the engine weapon string.
public class WeaponLockerGameEvent : ClientGameEvent
{
    public bool IsStore { get; init; }
    public string WeaponName { get; init; } = string.Empty;
}
