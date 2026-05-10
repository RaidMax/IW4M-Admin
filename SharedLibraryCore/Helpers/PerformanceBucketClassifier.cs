using Data.Abstractions;
using Data.Models.Server;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Helpers;

/// <summary>
/// Determines whether a performance bucket should be treated as a "zombies bucket"
/// based on the live server population, replacing the legacy hardcoded literal
/// "zombies" bucket-code check. A bucket counts as zombies when more than
/// <see cref="DefaultZombieRatioThreshold"/> of the live servers tagged to that
/// bucket pass <see cref="Utilities.IsZombieServer(IGameServer)"/>.
///
/// Single source of truth for both base Stats (StatManager) and the premium
/// ZombieStats plugin so admins can name buckets freely (e.g. "T4 - Zombies",
/// "Comp Zombies") without losing zombie-specific top-stats columns or the
/// KDR-suppression rule.
/// </summary>
public static class PerformanceBucketClassifier
{
    public const double DefaultZombieRatioThreshold = 0.75;

    public readonly record struct BucketClassification(
        bool IsZombieBucket,
        IReadOnlySet<long> ZombieServerIds);

    public static async Task<BucketClassification> ClassifyAsync(
        IManager manager,
        IDatabaseContextFactory contextFactory,
        string bucketCode,
        double threshold = DefaultZombieRatioThreshold,
        CancellationToken cancellationToken = default)
    {
        var normalized = PerformanceBucketCodes.Normalize(bucketCode);
        var liveServers = manager.GetServers();
        if (liveServers.Count == 0)
        {
            return new BucketClassification(false, new HashSet<long>());
        }

        var liveIds = liveServers.Select(s => s.LegacyDatabaseId).ToList();
        await using var ctx = contextFactory.CreateContext(false);
        var bucketCodes = await ctx.Set<EFServer>()
            .Where(s => liveIds.Contains(s.ServerId))
            .Select(s => new { s.ServerId, Code = s.PerformanceBucket != null ? s.PerformanceBucket.Code : null })
            .ToDictionaryAsync(x => x.ServerId, x => PerformanceBucketCodes.Normalize(x.Code), cancellationToken);

        var inBucket = liveServers
            .Where(s => bucketCodes.TryGetValue(s.LegacyDatabaseId, out var c) && c == normalized)
            .ToList();

        if (inBucket.Count == 0)
        {
            return new BucketClassification(false, new HashSet<long>());
        }

        var zombieIds = inBucket
            .Where(s => s.IsZombieServer())
            .Select(s => s.LegacyDatabaseId)
            .ToHashSet();

        var ratio = (double)zombieIds.Count / inBucket.Count;
        var isZombieBucket = ratio > threshold;
        return new BucketClassification(isZombieBucket, zombieIds);
    }
}
