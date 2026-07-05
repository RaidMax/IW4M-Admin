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

    /// <summary>
    /// Round number captured at EE-fire time. Null when EE hasn't fired or
    /// RoundNumber was 0/indeterminate at fire time.
    /// </summary>
    public int? EasterEggRound { get; set; }

    /// <summary>
    /// UTC timestamp captured at EE-fire time. Authoritative "EE fired" signal —
    /// presence is used to gate re-emit and to flag persistence. Flushed to
    /// <see cref="ZombieMatch.EasterEggOccurredAt"/> on match-end persist.
    /// </summary>
    public DateTimeOffset? EasterEggOccurredAt { get; set; }

    /// <summary>
    /// Step keys logged for this match's EE-progress tracking. Match-level cache
    /// for in-process dedup (avoid double-fire from engine notify quirks) and for
    /// the "all steps fired" derivation of <see cref="EasterEggOccurredAt"/> on
    /// maps without a canonical terminal notify. Persisted state lives in the
    /// event log; this is the runtime mirror.
    /// </summary>
    public HashSet<string> EasterEggStepsLogged { get; } = [];

    /// <summary>
    /// Quest ids (per <c>MapEasterEggConfig</c>) that have completed during this
    /// match, either via the canonical terminal notify or via "all steps logged"
    /// derivation. Match-level cache to gate canonical re-emits and re-derivations.
    /// For branching quests, holds the variant id (e.g. "transit_maxis"), not the
    /// group id — completion is per-variant and the group's "the EE happened in
    /// this match" signal is whichever variant lands first.
    /// </summary>
    public HashSet<string> EasterEggQuestsCompleted { get; } = [];

    /// <summary>
    /// Hard-lock map: branching-quest group id → variant id of the first variant
    /// to fire any step in the current match. Subsequent steps from sibling
    /// variants are rejected at the writer (see ZombieEventProcessor.OnEasterEggStep)
    /// — Maxis-then-Richtofen mid-match is impossible in the GSC (power state
    /// gates), so any cross-variant step is treated as bad data and dropped.
    /// Transient match-only state; not persisted (BuildQuests recovers the
    /// active variant from step records at read time).
    /// </summary>
    public Dictionary<string, string> EasterEggLockedVariantByGroup { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Player count captured at each round's start. Keyed by RoundNumber; value is the
    /// count of qualifying clients when the round began (zombie spawn count is fixed
    /// for the round at this number, regardless of mid-round joins/leaves). Used by
    /// the round-duration EMA to key its (Map, Round, PlayerCount) cell so solo and
    /// co-op rounds normalize against their own cohorts.
    /// </summary>
    public Dictionary<int, int> RoundPlayerCounts { get; } = new();

    /// <summary>
    /// Special-round type per round number — populated from GSC
    /// <c>GSE;ZW;round_special;&lt;round&gt;;&lt;type&gt;</c> emissions. Absence = normal round. Drives the
    /// Round Breakdown UI badge and the !ztimings SPH gate (special rounds replace
    /// the regular spawn budget so the static SPH formula doesn't apply). Mid-round
    /// mini-bosses (panzer/brutus/mechz/ghost/sloth) are deliberately NOT recorded
    /// here — those add a small fixed enemy count alongside regular zombies; SPH
    /// stays approximately correct.
    /// </summary>
    public Dictionary<int, ZombieSpecialRoundType> RoundSpecialTypes { get; } = new();

    /// <summary>
    /// Latest engine snapshot for the current round: zombies still to be SPAWNED
    /// (level.zombie_total). Updated by the GSC <c>WatchZombiesRemaining</c> watcher
    /// every ~2s. Null between match start and the first ZR emission, or when the
    /// emitted RoundNumber doesn't match the live round (stale arrival ignored).
    /// Drives the live-modal SPH calculation: cleared = budget − remaining − alive.
    /// </summary>
    public int? CurrentRoundZombiesRemaining { get; set; }

    /// <summary>
    /// Latest engine snapshot for the current round: zombies currently alive on the
    /// map (get_enemy_count / get_current_zombie_count). Paired with
    /// <see cref="CurrentRoundZombiesRemaining"/>. Null until the first ZR emission.
    /// </summary>
    public int? CurrentRoundZombiesAlive { get; set; }
}
