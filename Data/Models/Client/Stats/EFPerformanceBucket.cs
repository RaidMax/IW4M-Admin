using System.ComponentModel.DataAnnotations;

namespace Data.Models.Client.Stats;

/// <summary>
/// Groups game servers into isolated ranking pools so that different game modes
/// (e.g. multiplayer, zombies, competitive) maintain separate leaderboards and
/// performance distributions. Each server is optionally assigned to a bucket via
/// <see cref="EFServer.PerformanceBucketId"/>; servers without a bucket use global defaults.
/// Rankings, Z-scores, and hit statistics are all scoped per-bucket.
/// </summary>
public class EFPerformanceBucket
{
    [Key] public int PerformanceBucketId { get; set; }

    /// <summary>
    /// Unique string identifier used in configuration and queries (e.g. "zombies", "competitive").
    /// Matched against <see cref="SharedLibraryCore.Configuration.ServerConfiguration.PerformanceBucketCode"/>.
    /// <para>
    /// <b>Stored canonical form: lower-case.</b> The writer in
    /// <c>IW4MServer.UpdatePerformanceBucket</c> lower-cases on insert (and
    /// <see cref="SharedLibraryCore.Server.PerformanceCode"/> normalises the
    /// runtime value), so case-sensitive equality comparisons against this
    /// column must use a lower-case comparand. Code-side, run any user-supplied
    /// bucket code through <c>SharedLibraryCore.Helpers.PerformanceBucketCodes.Normalize</c>
    /// before comparing — settings.json lets users write capitalised codes
    /// (e.g. <c>"Zombies"</c>) for readability, but the DB only sees the
    /// normalised form.
    /// </para>
    /// </summary>
    [MaxLength(256)]
    public string Code { get; set; }

    /// <summary>
    /// Human-readable display name shown in the web UI and leaderboards.
    /// </summary>
    [MaxLength(256)]
    public string Name { get; set; }
}
