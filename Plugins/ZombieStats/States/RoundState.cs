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

    /// <summary>
    /// Number of qualifying (kill-recording) players in the match at the moment this
    /// round began. Frozen at round-start so the EMA cell key matches the round's
    /// fixed zombie spawn count, regardless of mid-round joins/leaves.
    /// </summary>
    public int PlayerCountAtRoundStart { get; set; }
}
