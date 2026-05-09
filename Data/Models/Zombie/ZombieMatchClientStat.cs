using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Models.Zombie;

public class ZombieMatchClientStat : ZombieClientStat
{
    [NotMapped] public int? JoinedRound { get; set; }
    [NotMapped] public int? LastRoundReached { get; set; }

    /// <summary>
    /// True when the row was created by carryover (auto-enrollment of an already-Connected
    /// client at match-create) but the client hasn't yet emitted any per-client event.
    /// Tentative rows are NOT persisted to DB and do NOT bump lifetime TotalMatchesPlayed.
    /// First per-client event in <c>RunCalculation</c> calls <c>PromoteTentativeIfNeeded</c>
    /// to clear the flag, persist the row, and bump lifetime stats. Clients who untrack
    /// while still tentative are dropped entirely (no DB row, no lifetime pollution).
    /// Defends against the carryover-of-leavers phantom pattern (see match 524).
    /// </summary>
    [NotMapped] public bool IsTentative { get; set; }

    /// <summary>
    /// Number of rounds in this match where this player had at least one other tracked
    /// player alongside them. Computed in <c>FinalizePlayerCountAsync</c> after match end.
    /// Null on legacy rows that pre-date the badge feature.
    /// </summary>
    public int? AssistedRounds { get; set; }

    /// <summary>
    /// First round at which this player became "solo to the end" — every round from this
    /// number through the match's HighestRound was played alone. Null if the player was
    /// either solo throughout (no assistance ever) OR was assisted up to and including the
    /// final round. The "Solo from R&lt;N&gt;" badge uses this as both its trigger and its
    /// display value. Null on legacy rows.
    /// </summary>
    public int? SoloFromRound { get; set; }
}
