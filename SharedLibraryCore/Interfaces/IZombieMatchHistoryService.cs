#nullable enable

namespace SharedLibraryCore.Interfaces;

/// <summary>
/// Provides per-player zombie match history with round breakdowns and event timelines.
/// Implemented by the premium plugin.
/// </summary>
public interface IZombieMatchHistoryService
{
    Task<List<ZombieMatchHistoryMatch>> GetPlayerMatchHistoryAsync(int clientId, string? serverEndpoint,
        int offset = 0, int count = 5);

    /// <summary>
    /// Returns full match detail with all players' round breakdowns and event timelines.
    /// Used by the leaderboard's expandable match detail view.
    /// </summary>
    Task<ZombieMatchDetail?> GetMatchDetailAsync(int matchId);
}

public class ZombieMatchDetail
{
    public int MatchId { get; set; }
    public string Map { get; set; } = string.Empty;
    public DateTimeOffset Date { get; set; }
    public string? ServerName { get; set; }
    public double DurationMinutes { get; set; }
    public int HighestRound { get; set; }
    public bool Completed { get; set; }
    public List<ZombieMatchDetailPlayer> Players { get; set; } = [];

    /// <summary>
    /// Distinct buildables completed in this match. Sourced from event-log entries
    /// of type <c>BuildComplete</c> with the buildable name in <c>TextualValue</c>.
    /// </summary>
    public int BuildablesBuilt { get; set; }

    /// <summary>
    /// Total iconic buildables on this map (per <c>MapBuildableConfig</c>), or null
    /// when the map has no configured total — custom maps, T4/T5 (no buildable
    /// system), or unmapped maps. Null hides the denominator in the UI.
    /// </summary>
    public int? BuildablesTotal { get; set; }

    /// <summary>
    /// Names of buildables completed (distinct, in build order). Used by the
    /// dedicated share page's rich card; the leaderboard compact badge ignores it.
    /// </summary>
    public List<string> BuildableNames { get; set; } = [];

    /// <summary>Round at which the EE fired (when known).</summary>
    public int? EasterEggRound { get; set; }

    /// <summary>
    /// UTC timestamp at which the EE fired. Authoritative "EE happened" signal —
    /// non-null implies completed. Drives the scrubber timeline marker.
    /// </summary>
    public DateTimeOffset? EasterEggOccurredAt { get; set; }
}

public class ZombieMatchDetailPlayer
{
    public int ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Downs { get; set; }
    public int Revives { get; set; }
    public long PointsEarned { get; set; }
    public int Headshots { get; set; }
    public long DamageDealt { get; set; }
    public int DamageReceived { get; set; }

    /// <summary>
    /// Rounds in this match where this player had at least one other tracked teammate.
    /// Null on legacy matches.
    /// </summary>
    public int? AssistedRounds { get; set; }

    /// <summary>
    /// First round at which the player became "solo to the end". Drives the
    /// "Solo from R&lt;N&gt;" badge. Null if the player was assisted to the final round.
    /// </summary>
    public int? SoloFromRound { get; set; }

    public List<ZombieMatchHistoryRound> Rounds { get; set; } = [];
    public List<ZombieMatchHistoryEvent> Events { get; set; } = [];
}

public class ZombieMatchHistoryMatch
{
    public int MatchId { get; set; }
    public string Map { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string? ServerName { get; set; }
    public int HighestRound { get; set; }
    public double DurationMinutes { get; set; }
    public long Kills { get; set; }
    public long Deaths { get; set; }
    public long PointsEarned { get; set; }
    public bool Completed { get; set; }

    /// <summary>Round at which the EE fired (when known).</summary>
    public int? EasterEggRound { get; set; }

    /// <summary>
    /// UTC timestamp at which the EE fired. Non-null implies completed.
    /// </summary>
    public DateTimeOffset? EasterEggOccurredAt { get; set; }

    /// <summary>Distinct buildables completed in this match.</summary>
    public int BuildablesBuilt { get; set; }

    /// <summary>Total iconic buildables on this map (per <c>MapBuildableConfig</c>), or null.</summary>
    public int? BuildablesTotal { get; set; }

    /// <summary>This player's total downs across the match — drives the "Personal No-Down" badge.</summary>
    public int Downs { get; set; }

    /// <summary>
    /// Rounds in this match where this player had at least one other tracked teammate.
    /// Null on legacy matches that pre-date the metric.
    /// </summary>
    public int? AssistedRounds { get; set; }

    /// <summary>
    /// First round at which the player became "solo to the end". Drives the
    /// "Solo from R&lt;N&gt;" badge. Null if the player was assisted to the final round
    /// or the metric wasn't computed for this match (legacy data).
    /// </summary>
    public int? SoloFromRound { get; set; }

    public List<ZombieMatchHistoryRound> Rounds { get; set; } = [];
    public List<ZombieMatchHistoryEvent> Events { get; set; } = [];
}

public class ZombieMatchHistoryRound
{
    public int RoundNumber { get; set; }
    public long Kills { get; set; }
    public long Deaths { get; set; }
    public long Downs { get; set; }
    public long Revives { get; set; }
    public int Points { get; set; }
    public double DurationSeconds { get; set; }
}

/// <summary>
/// A pre-processed, display-ready event for the match timeline.
/// All mapping from internal event types to labels/categories is done by the provider.
/// </summary>
public class ZombieMatchHistoryEvent
{
    /// <summary>Display time string (e.g. "20:03:05").</summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>Time as total seconds from midnight, for timeline positioning.</summary>
    public double Seconds { get; set; }

    /// <summary>Human-readable label (e.g. "Downed", "Double Points", "Round 5").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Visual category for rendering. One of: "round", "powerup", "danger", "critical", "success",
    /// "perk", "weapon", "box", "box-pass", "door", "trap", "build", "session-join", "session-leave".
    /// The component maps these to colors/icons.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// For round-category events, the round number this marker represents. Lets the
    /// timeline component compute gap ranges (where the player skipped rounds) without
    /// parsing the <see cref="Label"/> string. Null for non-round events.
    /// </summary>
    public int? RoundNumber { get; set; }
}
