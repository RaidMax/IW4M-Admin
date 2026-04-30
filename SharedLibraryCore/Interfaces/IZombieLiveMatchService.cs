#nullable enable

namespace SharedLibraryCore.Interfaces;

/// <summary>
/// Read-only access to the in-memory zombie match state for a server, for "live snapshot"
/// UIs (e.g. the home-page server card's skull-icon modal). All data is assembled from
/// in-memory state (authoritative for current values) plus DB-backed historical context
/// (recent events, completed rounds). Returns null when the server has no active match.
/// Implemented by the premium plugin.
/// </summary>
public interface IZombieLiveMatchService
{
    Task<ZombieLiveMatchSnapshot?> GetLiveMatchSnapshotAsync(string serverId);
}

public class ZombieLiveMatchSnapshot
{
    public int MatchId { get; set; }
    public string Map { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public int CurrentRound { get; set; }
    public DateTimeOffset MatchStartedAt { get; set; }

    /// <summary>
    /// Wall-clock timestamp at which the current round started — drives the
    /// per-round timer in the live modal. Null if the round-start time isn't
    /// available (e.g. immediately after a stitching resume, before the first
    /// post-resume RC event arrives).
    /// </summary>
    public DateTimeOffset? CurrentRoundStartedAt { get; set; }

    public List<ZombieLivePlayerSnapshot> Players { get; set; } = [];

    /// <summary>
    /// Recent timeline events — current round + ~5 from previous round for context.
    /// Same shape as the post-match timeline so the modal can reuse rendering helpers.
    /// </summary>
    public List<ZombieMatchHistoryEvent> RecentEvents { get; set; } = [];

    /// <summary>
    /// Round at which the EE-complete event fired during this live match, if any.
    /// Null when EE hasn't fired yet (or the map has no watcher). Surfaced as a
    /// "EE R{n}" badge in the live modal.
    /// </summary>
    public int? EasterEggRound { get; set; }

    /// <summary>
    /// Per-quest EE progress for this map. Empty when no configured quests. Each entry
    /// drives one chip + mini progress strip in the live modal.
    /// </summary>
    public List<EasterEggQuestProgress> EasterEggQuests { get; set; } = [];
}

/// <summary>
/// Live "alive state" for a player. Drives the badge in each player card.
/// Disconnected supersedes everything else (ghost players are pruned from the
/// snapshot after a TTL anyway).
/// </summary>
public enum ZombieLivePlayerStatus
{
    Alive,
    Down,
    Dead,
    Disconnected
}

public class ZombieLivePlayerSnapshot
{
    public int ClientId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>True when the client is in the server's current ConnectedClients list.</summary>
    public bool IsConnected { get; set; }

    /// <summary>Combined alive/down/dead/disconnected status — drives the per-player badge.</summary>
    public ZombieLivePlayerStatus Status { get; set; } = ZombieLivePlayerStatus.Alive;

    /// <summary>The round at which this player first appeared in the match (joined-late detection).</summary>
    public int? JoinedRound { get; set; }

    /// <summary>Kills accumulated this round only — resets on round transition.</summary>
    public int CurrentRoundKills { get; set; }
    public int CurrentRoundDowns { get; set; }
    public int CurrentRoundDeaths { get; set; }

    /// <summary>Cumulative match stats — kept in sync with persisted EFZombieMatchClientStats.</summary>
    public int MatchKills { get; set; }
    public int MatchDeaths { get; set; }
    public int MatchDowns { get; set; }
    public int MatchRevives { get; set; }
    public long MatchPointsEarned { get; set; }
    public long MatchPointsSpent { get; set; }
    public long MatchDamageDealt { get; set; }
    public long MatchDamageReceived { get; set; }
    public int MatchHeadshotKills { get; set; }

    /// <summary>Per-round breakdown for collapsible drill-down. Populated for completed rounds only.</summary>
    public List<ZombieMatchHistoryRound> RoundsCompleted { get; set; } = [];
}
