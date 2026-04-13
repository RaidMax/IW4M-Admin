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
    public string MatchDate { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public string? Duration { get; set; }
    public string? ServerName { get; set; }
    public List<ZombieLeaderboardPlayer> Players { get; set; } = [];
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
}
