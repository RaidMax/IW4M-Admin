using System;
using SharedLibraryCore;

namespace Stats.Config;

/// <summary>
/// Per-bucket settings that control how players are ranked within a performance bucket.
/// Configured in <see cref="StatsConfiguration.PerformanceBuckets"/>.
/// If a server's bucket code doesn't match any configured entry, defaults are used.
/// </summary>
public class PerformanceBucketConfiguration
{
    /// <summary>
    /// Matches <see cref="Data.Models.Client.Stats.EFPerformanceBucket.Code"/> to link
    /// this configuration to its database bucket entity.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Minimum total playtime before a player is eligible for ranking in this bucket.
    /// Prevents new or low-activity players from appearing on leaderboards.
    /// </summary>
    public TimeSpan ClientMinPlayTime { get; set; } = Utilities.IsDevelopment ? TimeSpan.FromMinutes(1) : TimeSpan.FromHours(3);

    /// <summary>
    /// How far back to look when computing rankings. Stats older than this are excluded
    /// from Z-score and leaderboard calculations, keeping rankings current.
    /// </summary>
    public TimeSpan RankingExpiration { get; set; } = TimeSpan.FromDays(15);
}
