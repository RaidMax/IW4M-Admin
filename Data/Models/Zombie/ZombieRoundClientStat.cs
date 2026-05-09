using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Models.Zombie;

public class ZombieRoundClientStat : ZombieClientStat
{
    public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public TimeSpan? TimeAlive { get; set; }
    public int RoundNumber { get; set; }
    public int Points { get; set; }

    /// <summary>
    /// Number of qualifying players in the match at the moment this round began.
    /// Persisted snapshot of <c>RoundState.PlayerCountAtRoundStart</c> so the
    /// round can be cross-referenced against the right EMA cell at read time
    /// (drives match-detail / leaderboard pace tinting). Null on legacy rows
    /// pre-dating this column — readers fall back to <c>EFZombieMatch.PlayerCount</c>.
    /// </summary>
    public int? PlayerCountAtRoundStart { get; set; }

    /// <summary>
    /// Mirrors <c>ZombieMatchClientStat.IsTentative</c>: round entries created by
    /// <c>StartNextRound</c> for a tentative match-stat are also tentative until the
    /// first event promotes the whole tree. Skipped from DB persistence while tentative.
    /// </summary>
    [NotMapped] public bool IsTentative { get; set; }
}
