namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

// Fires when a player commits to a Pack-a-Punch buy (5000 points deducted,
// weapon placed in machine). Outcome distinguishes whether the player took
// the upgraded weapon (Upgrade — engine pap_taken notify) or walked away
// before the timeout (Abandon — engine pap_timeout notify).
//
// Matches the BoxUseGameEvent pattern — single event class with an outcome
// enum rather than separate classes per outcome.
public class PackAPunchGameEvent : ClientGameEvent
{
    public enum PaPOutcome
    {
        // Player took the upgraded weapon. NewWeapon is set.
        Upgrade,
        // Player walked away; engine timed out. NewWeapon is null/empty.
        Abandon,
    }

    public PaPOutcome Outcome { get; init; }

    // The weapon the player put into the machine.
    public string OldWeapon { get; init; }

    // The upgraded weapon the player received. Only populated for Outcome=Upgrade
    // (= OldWeapon + "_upgraded" by Treyarch convention). Null for Abandon.
    public string? NewWeapon { get; init; }

    public int Cost { get; init; }
}
