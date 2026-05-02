using System;
using SharedLibraryCore;

namespace Stats.Config;

/// <summary>
/// Canonical handling for performance-bucket codes. A null/empty code in
/// <c>IW4MAdminSettings.PerformanceBucketCode</c> represents the implicit
/// "default" bucket — same logical pool as a server explicitly tagged
/// <c>"default"</c>. Centralising the normalisation here avoids the
/// historical inconsistency where seed loops, query filters, and writers
/// each picked a different fallback (<c>"null"</c> vs <c>""</c> vs SQL
/// NULL FK) and silently produced empty leaderboards.
/// </summary>
public static class PerformanceBucketCodes
{
    public const string Default = "default";

    /// <summary>True when <paramref name="code"/> represents the default bucket
    /// (null, empty, or the literal "default" in any casing).</summary>
    public static bool IsDefault(string code) =>
        string.IsNullOrEmpty(code) || string.Equals(code, Default, StringComparison.OrdinalIgnoreCase);

    /// <summary>Lower-cases the code (the DB writer in IW4MServer normalises on insert)
    /// and collapses null/empty to <see cref="Default"/>. All cache keys, DB filter
    /// values, and writer FK lookups must run through this.</summary>
    public static string Normalize(string code) =>
        string.IsNullOrEmpty(code) ? Default : code.ToLowerInvariant();
}

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
