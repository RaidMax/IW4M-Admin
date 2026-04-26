using Data.Models.Client.Stats;
using Data.Models.Zombie;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.ZombieStats.States;

public record MatchState(IGameServer Server, ZombieMatch PersistentMatch)
{
    /// <summary>
    /// Per-round client state, keyed by (NetworkId, RoundNumber). Composite key lets
    /// late-arriving end-of-round (RD) events find their original round entry even
    /// after StartNextRound has advanced to the next round — eliminates the
    /// Clear/repopulate race that produced Points=0 and zero-duration rounds.
    /// Old-round entries remain until <see cref="ZombieClientStateManager.EndMatch"/>;
    /// memory cost is bounded by rounds × players per match.
    /// </summary>
    public Dictionary<(long NetworkId, int RoundNumber), RoundState> RoundStates { get; } = new();
    public Dictionary<long, ZombieMatchClientStat> PersistentMatchAggregateStats { get; } = new();
    public Dictionary<long, ZombieAggregateClientStat> PersistentLifetimeAggregateStats { get; } = new();
    public Dictionary<long, ZombieAggregateClientStat> PersistentLifetimeServerAggregateStats { get; } = new();
    public Dictionary<long, Dictionary<string, EFClientStatTagValue>> PersistentStatTagValues { get; } = new();
    public int RoundNumber { get; set; }

    /// <summary>
    /// Timestamp of the last <c>StartNextRound</c> for this match.
    /// Diagnostic-only — surfaced in "Missing state data" warnings to correlate
    /// an RD failure with a recent round transition that may have wiped state.
    /// </summary>
    public DateTimeOffset? LastStartNextRoundUtc { get; set; }

    /// <summary>
    /// True when this match was created by a mid-match bootstrap path
    /// (TrackClient finding no active match) and the dvar query returned
    /// no usable round number, so the round / JoinedRound seeds may be wrong.
    /// Cleared when a real RC event arrives and force-rebases the match.
    /// </summary>
    public bool BootstrapPendingFirstRound { get; set; }

    /// <summary>
    /// First-observed disconnection timestamp per NetworkId. Populated lazily by the
    /// live-snapshot service when a tracked player no longer appears in the server's
    /// ConnectedClients list. Cleared on reconnect. Used to age disconnected players
    /// out of the live modal after a TTL — they linger briefly so a brief drop is
    /// visible, then disappear so the modal doesn't accumulate ghosts.
    /// </summary>
    public Dictionary<long, DateTimeOffset> DisconnectedFirstSeen { get; } = new();
}
