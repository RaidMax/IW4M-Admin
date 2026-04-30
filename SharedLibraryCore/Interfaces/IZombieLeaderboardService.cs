#nullable enable
using Data.Models;

namespace SharedLibraryCore.Interfaces;

/// <summary>
/// Provides zombie leaderboard data. Implemented by the premium plugin.
/// When not registered in DI, the leaderboard page returns 404.
/// </summary>
public interface IZombieLeaderboardService
{
    /// <summary>
    /// Returns the available games, maps, and player counts for the leaderboard navigation.
    /// </summary>
    Task<ZombieLeaderboardMetadata> GetLeaderboardMetadataAsync();

    /// <summary>
    /// Returns paginated leaderboard entries for a specific game/map/player-count combination.
    /// </summary>
    Task<ZombieLeaderboardResponse> GetLeaderboardEntriesAsync(
        Reference.Game game, int mapId, int playerCount,
        int offset, int count);

    /// <summary>
    /// Returns the stat records for a specific map (across all player counts).
    /// </summary>
    Task<List<ZombieMapStatRecord>> GetMapRecordsAsync(Reference.Game game, int mapId);
}

public class ZombieMapStatRecord
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public int ClientId { get; set; }
}

public class ZombieLeaderboardMetadata
{
    public List<ZombieLeaderboardGame> Games { get; set; } = [];
}

public class ZombieLeaderboardGame
{
    public Reference.Game Game { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public List<ZombieLeaderboardMap> Maps { get; set; } = [];
}

public class ZombieLeaderboardMap
{
    public int MapId { get; set; }
    public string MapName { get; set; } = string.Empty;
    public List<int> PlayerCounts { get; set; } = [];
}

public class ZombieLeaderboardResponse
{
    public List<ZombieLeaderboardEntry> Entries { get; set; } = [];
    public int TotalCount { get; set; }
}

public class ZombieLeaderboardEntry
{
    public int Rank { get; set; }
    public int MatchId { get; set; }
    public int HighestRound { get; set; }
    public DateTimeOffset MatchDate { get; set; }
    public string MapName { get; set; } = string.Empty;
    public string? Duration { get; set; }
    public string? ServerName { get; set; }
    public List<ZombieLeaderboardPlayer> Players { get; set; } = [];

    /// <summary>
    /// Total distinct players that participated in the match (any round entry),
    /// before the leaderboard qualifier filtered the list. When this exceeds
    /// <see cref="Players"/>.Count, the bucket entry is showing fewer players
    /// than were actually in the match (e.g. a 2-player match where one player
    /// joined too late or played too few rounds to qualify lands in the 1-player
    /// bucket). The card surfaces this so viewers know the entry isn't a true
    /// solo run. Equal to <see cref="Players"/>.Count for honest entries.
    /// </summary>
    public int TotalPlayerCount { get; set; }

    /// <summary>Round at which the EE fired (when known). Drives the "EE R{n}" titlebar badge.</summary>
    public int? EasterEggRound { get; set; }

    /// <summary>Per-quest EE progress for this match's map. Empty when unconfigured.</summary>
    public List<EasterEggQuestProgress> EasterEggQuests { get; set; } = [];

    /// <summary>
    /// Iconic buildables completed (distinct, capped at <see cref="BuildablesTotal"/>).
    /// Drives the "All Built" titlebar badge when equal to <see cref="BuildablesTotal"/>.
    /// On unconfigured maps falls back to all distinct names built.
    /// </summary>
    public int BuildablesBuilt { get; set; }

    /// <summary>
    /// Total iconic buildables on this map (per <c>MapBuildableConfig</c>), or null when
    /// the map has no configured total. Null hides the All-Built badge.
    /// </summary>
    public int? BuildablesTotal { get; set; }

    /// <summary>
    /// Distinct non-iconic buildables completed (side-quest / extras). Drives the "+N
    /// extra" indicator alongside the All-Built badge. Always 0 on unconfigured maps.
    /// </summary>
    public int ExtraBuildablesBuilt { get; set; }

    /// <summary>
    /// Total downs across all qualified players in this match. Drives the
    /// "No-Down" titlebar badge when zero (and <see cref="HighestRound"/> ≥ 5
    /// to filter trivially short matches).
    /// </summary>
    public int TotalDowns { get; set; }
}

public class ZombieLeaderboardPlayer
{
    public int ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Downs { get; set; }
    public int Revives { get; set; }
    public long PointsEarned { get; set; }
    public long PointsSpent { get; set; }
    public int HeadshotKills { get; set; }
    public long DamageDealt { get; set; }
    public int DamageReceived { get; set; }

    /// <summary>
    /// Rounds in this match where this player had at least one other tracked teammate.
    /// Null on legacy rows.
    /// </summary>
    public int? AssistedRounds { get; set; }

    /// <summary>
    /// First round at which this player became "solo to the end". Drives the
    /// "Solo from R&lt;N&gt;" badge on the leaderboard card. Null when player was assisted
    /// to the final round or the metric wasn't computed.
    /// </summary>
    public int? SoloFromRound { get; set; }
}
