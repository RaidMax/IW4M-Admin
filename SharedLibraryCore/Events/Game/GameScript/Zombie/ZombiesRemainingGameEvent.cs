namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

/// <summary>
/// Periodic engine snapshot of the current round's zombie state — emitted from
/// the GSC <c>WatchZombiesRemaining</c> watcher every ~2s during an active round.
///
/// Lets IW4MAdmin compute true "zombies cleared" = budget − remaining − alive,
/// which captures trap kills / environmental kills / friendly-grenade splash —
/// all things that don't credit to a player's kill count but still reduce the
/// round's spawn pool. Without this signal, the live-modal SPH calculation
/// systematically over-estimated pace on trap-heavy strategies.
/// </summary>
public class ZombiesRemainingGameEvent : GameEventV2
{
    /// <summary>Round number this snapshot belongs to. Lets the handler discard
    /// stale emissions from a previous round if they arrive after StartNextRound.</summary>
    public int RoundNumber { get; init; }

    /// <summary>
    /// Engine's <c>level.zombie_total</c> — count of zombies still to be SPAWNED
    /// this round. Set to the round's full spawn budget at round start, decrements
    /// as each zombie spawns. Reaches 0 once the spawner has dispatched everything;
    /// <see cref="Alive"/> may still be non-zero past that point.
    /// </summary>
    public int Remaining { get; init; }

    /// <summary>
    /// Engine's currently-alive zombie count (<c>get_enemy_count</c> on T4/T5,
    /// <c>get_current_zombie_count</c> on T6). Includes zombies in the process of
    /// despawning between rounds, so brief stale-positive values mid-transition are
    /// expected.
    /// </summary>
    public int Alive { get; init; }
}
