using Data.Models.Zombie;

namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

/// <summary>
/// Marks the round about to begin as a "special" round — one whose spawn budget
/// is decoupled from the normal <c>round_spawning</c> formula (dogs/leapers/
/// monkeys replace the entire round's zombie pool). Lets IW4MAdmin tag the
/// round in the breakdown UI and skip the static Seconds-Per-Horde calculation
/// for round types where the formula doesn't apply.
///
/// Mid-round mini-bosses (panzer, brutus, mechz, ghost, sloth) are NOT emitted
/// here — those add a small fixed enemy count alongside regular zombies; SPH
/// stays approximately correct without per-type compensation.
/// </summary>
public class RoundSpecialGameEvent : GameEventV2
{
    /// <summary>The round number this special-round designation applies to.</summary>
    public int RoundNumber { get; init; }

    /// <summary>
    /// Parsed special-round type. Null when the GSC emitted a token the parser
    /// doesn't yet recognise — handler treats unknown specials as no-op rather
    /// than crashing the event pipeline.
    /// </summary>
    public ZombieSpecialRoundType? SpecialType { get; init; }
}
