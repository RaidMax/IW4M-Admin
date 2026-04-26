using Data.Models.Zombie;

namespace IW4MAdmin.Plugins.ZombieStats.States;

public record RoundState
{
    public ZombieRoundClientStat PersistentClientRound { get; init; } = null!;
    public DateTimeOffset? DiedAt { get; set; }
    public int Hits { get; set; }

    /// <summary>
    /// True between a Downed event and a subsequent Revived event for this player
    /// in this round. Drives the live snapshot's per-player "down" status.
    /// Reset by a fresh round entry (next round, new RoundState).
    /// </summary>
    public bool IsDowned { get; set; }
}
