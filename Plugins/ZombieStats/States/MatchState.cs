using Data.Models.Client.Stats;
using Data.Models.Zombie;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.ZombieStats.States;

public record MatchState(IGameServer Server, ZombieMatch PersistentMatch)
{
    public Dictionary<long, RoundState> RoundStates { get; } = new();
    public Dictionary<long, ZombieMatchClientStat> PersistentMatchAggregateStats { get; } = new();
    public Dictionary<long, ZombieAggregateClientStat> PersistentLifetimeAggregateStats { get; } = new();
    public Dictionary<long, ZombieAggregateClientStat> PersistentLifetimeServerAggregateStats { get; } = new();
    public Dictionary<long, Dictionary<string, EFClientStatTagValue>> PersistentStatTagValues { get; } = new();
    public int RoundNumber { get; set; }

    /// <summary>
    /// Number of players who started the most recent round.
    /// Used for PlayerCount at match end — avoids edge cases with late disconnects.
    /// </summary>
    public int LastRoundPlayerCount { get; set; }

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
}
