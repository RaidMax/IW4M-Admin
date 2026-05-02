using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models.Client;
using Data.Models.Client.Stats;
using IW4MAdmin.Plugins.Stats;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using Stats.Client.Abstractions;
using Stats.Config;
using Stats.Helpers;

namespace Stats.Client
{
    /// <summary>
    /// Computes log-normal distribution parameters and Z-scores for player performance,
    /// scoped per server and per performance bucket. During initialization, it builds two caches:
    /// <list type="bullet">
    ///   <item><b>Distribution cache</b> — fits a log-normal (mean/sigma) to each server's and
    ///     each bucket's player performance values (⅓ Elo + ⅔ Skill). Refreshed hourly (1 min in dev).</item>
    ///   <item><b>Max Z-score cache</b> — tracks the highest playtime-weighted average Z-score per bucket,
    ///     used to normalize raw Z-scores into a 0–1 performance rating. Refreshed every 30 min.</item>
    /// </list>
    /// These caches are keyed by serverId (for per-server lookups) and by bucket code
    /// (for cross-server aggregate rankings within a bucket).
    /// </summary>
    public class ServerDistributionCalculator(
        IDatabaseContextFactory contextFactory,
        IDataValueCache<EFClientStatistics, Dictionary<string, Extensions.LogParams>> distributionCache,
        IDataValueCache<EFClientStatistics, double> maxZScoreCache,
        StatsConfiguration config,
        ApplicationConfiguration appConfig)
        : IServerDistributionCalculator
    {
        private readonly List<Tuple<long, string>> _serverIds = [];

        private const string DistributionCacheKey = nameof(DistributionCacheKey);
        private const string MaxZScoreCacheKey = nameof(MaxZScoreCacheKey);

        public async Task Initialize()
        {
            await LoadServers();

            distributionCache.SetCacheItem(async (set, token) =>
            {
                var distributions = new Dictionary<string, Extensions.LogParams>();

                await LoadServers();

                var iqPerformances = set
                    .Where(s => s.Skill > 0)
                    .Where(s => s.EloRating >= 0)
                    .Where(s => s.Client.Level != EFClient.Permission.Banned);

                foreach (var (serverId, performanceBucket) in _serverIds)
                { 
                    var bucketConfig =
                        config.PerformanceBuckets.FirstOrDefault(bucket =>
                            bucket.Code == performanceBucket) ?? new PerformanceBucketConfiguration();

                    var oldestPerf = DateTime.UtcNow - bucketConfig.RankingExpiration;
                    var performances = await iqPerformances.Where(s => s.ServerId == serverId)
                        .Where(s => s.TimePlayed >= bucketConfig.ClientMinPlayTime.TotalSeconds)
                        .Where(s => s.UpdatedAt >= oldestPerf)
                        .Select(s => s.EloRating * 1 / 3.0 + s.Skill * 2 / 3.0)
                        .ToListAsync(token);
                    
                    var distributionParams = performances.GenerateDistributionParameters();
                    distributions.Add(serverId.ToString(), distributionParams);
                }

                // Bucket-level distribution fits.
                //
                // DB stores PerformanceBucket.Code lower-cased (IW4MServer normalises
                // via ToLowerInvariant on insert), but IW4MAdminSettings may have
                // mixed case ("Zombies") or omit the code entirely. Both situations
                // historically produced empty caches because the iterator-side
                // fallback ("null", "") never matched the DB filter. PerformanceBucketCodes
                // collapses both to the canonical "default" string and lower-cases
                // explicit codes, making the cache key, the LINQ comparison, and
                // PerformanceBucketCodes.IsDefault all agree.
                //
                // The default-bucket OR-clause picks up legacy rows whose bucket FK
                // is still NULL (servers that never had PerformanceBucketCode set,
                // or were registered before the bucket column existed). Without this,
                // a freshly-deployed bucket-aware build sees zero default-bucket data
                // until every server is manually backfilled.
                foreach (var performanceBucket in appConfig.Servers
                             .Select(server => PerformanceBucketCodes.Normalize(server.PerformanceBucketCode))
                             .Distinct())
                {
                    var bucketConfig =
                        config.PerformanceBuckets.FirstOrDefault(bucket =>
                            string.Equals(bucket.Code, performanceBucket, StringComparison.OrdinalIgnoreCase))
                        ?? new PerformanceBucketConfiguration();

                    var isDefaultBucket = PerformanceBucketCodes.IsDefault(performanceBucket);
                    var oldestPerf = DateTime.UtcNow - bucketConfig.RankingExpiration;
                    var performances = await iqPerformances
                        .Where(perf => perf.Server.PerformanceBucket.Code == performanceBucket
                                       || (isDefaultBucket && perf.Server.PerformanceBucketId == null))
                        .Where(perf => perf.TimePlayed >= bucketConfig.ClientMinPlayTime.TotalSeconds)
                        .Where(perf => perf.UpdatedAt >= oldestPerf)
                        .Where(perf => perf.Skill < 999999)
                        .Select(s => s.EloRating * 1 / 3.0 + s.Skill * 2 / 3.0)
                        .ToListAsync(token);
                    var distributionParams = performances.GenerateDistributionParameters();
                    distributions.Add(performanceBucket, distributionParams);
                }

                return distributions;
            }, DistributionCacheKey, Utilities.IsDevelopment ? TimeSpan.FromMinutes(1) : TimeSpan.FromHours(1));

            // The maxZScore cache must be seeded after the distribution cache because
            // its callback now reads from distributionCache to derive z-scores from the
            // bucket distribution (rather than from the per-server EFClientStatistics.ZScore
            // column — see callback comment for the full reasoning). Force the
            // distribution cache populated first so each maxZScore seed sees fresh data.
            await distributionCache.GetCacheItem(DistributionCacheKey, CancellationToken.None);

            // Per-bucket max-z seed.
            //
            // Replaces the previous implementation, which read EFClientStatistics.ZScore
            // (per-server fits) and took the max of playtime-weighted averages across
            // a bucket. That model conflated per-server distributions: a single
            // low-population server with a degenerate sigma fit produced per-row
            // z-scores in the hundreds, and the aggregation pulled that outlier into
            // the bucket-wide max. The poisoned max became the rating denominator
            // (Rating = (z+3) / (max+z+3) * 1000), compressing every other player's
            // rating in the bucket to single-digit percent.
            //
            // The new model derives max-z from the bucket-fitted log-normal in
            // distributionCache: take the playtime-weighted Performance per qualifying
            // client across the bucket, find the maximum, transform to z once. This
            // gives a max in the same z-space the aggregate writer produces, with no
            // dependence on the per-server ZScore column. A pathological per-server
            // fit can no longer leak into bucket math.
            foreach (var performanceBucket in appConfig.Servers
                         .Select(s => PerformanceBucketCodes.Normalize(s.PerformanceBucketCode))
                         .Distinct())
            {
                maxZScoreCache.SetCacheItem(async (set, ids, token) =>
                    {
                        var localPerformanceBucket = (string)ids.FirstOrDefault();
                        var isDefaultBucket = PerformanceBucketCodes.IsDefault(localPerformanceBucket);

                        // Resolve the same min-playtime / recency window the aggregate
                        // writer uses — keeps the cache and the write path consistent.
                        var validPlayTime = config.TopPlayersMinPlayTime;
                        var oldestStatWindow = TimeSpan.FromDays(15);
                        var bucketConfig = config.PerformanceBuckets.FirstOrDefault(cfg =>
                            string.Equals(cfg.Code, localPerformanceBucket, StringComparison.OrdinalIgnoreCase));
                        if (bucketConfig is not null)
                        {
                            validPlayTime = (int)bucketConfig.ClientMinPlayTime.TotalSeconds;
                            oldestStatWindow = bucketConfig.RankingExpiration;
                        }

                        var distributions = await distributionCache.GetCacheItem(DistributionCacheKey, token);
                        if (!distributions.TryGetValue(localPerformanceBucket, out var bucketDist) || bucketDist.Sigma == 0)
                        {
                            // No bucket distribution available (cold start or empty bucket).
                            // Leave max as 0; the rating writer treats max==0 as "not ready"
                            // and skips the aggregate insert until the next refresh. Rare
                            // and self-healing.
                            return 0.0;
                        }

                        var oldestUpdated = DateTime.UtcNow - oldestStatWindow;
                        var maxWeightedPerformance = await set
                            .Where(stat => stat.Skill > 0)
                            .Where(stat => stat.EloRating >= 0)
                            .Where(stat => stat.Skill < 999999)
                            .Where(stat => stat.Client.Level != EFClient.Permission.Banned)
                            .Where(stat => stat.TimePlayed >= validPlayTime)
                            .Where(stat => stat.UpdatedAt >= oldestUpdated)
                            .Where(stat => stat.Server.PerformanceBucket.Code == localPerformanceBucket
                                           || (isDefaultBucket && stat.Server.PerformanceBucketId == null))
                            .GroupBy(stat => stat.ClientId)
                            // Performance is [NotMapped] so we recompute the composite inline
                            // (same formula as the distribution fit and the aggregate writer's
                            // ranking comparison — three call sites must agree exactly).
                            .Select(g => g.Sum(s => (s.EloRating / 3.0 + s.Skill * 2.0 / 3.0) * s.TimePlayed)
                                         / g.Sum(s => s.TimePlayed))
                            .MaxAsync(p => (double?)p, token);

                        if (maxWeightedPerformance is null or <= 0)
                        {
                            return 0.0;
                        }

                        // Single transform to z-space via the bucket distribution. Negative
                        // z would mean every qualifying client is below the bucket mean —
                        // floor at 0 so the rating denominator stays sane.
                        var maxZ = (Math.Log(maxWeightedPerformance.Value) - bucketDist.Mean) / bucketDist.Sigma;
                        return Math.Max(0.0, maxZ);
                    }, MaxZScoreCacheKey, new[] { performanceBucket },
                    Utilities.IsDevelopment ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(30));

                await maxZScoreCache.GetCacheItem(MaxZScoreCacheKey, new[] { performanceBucket });
            }

            /*foreach (var serverId in _serverIds)
            {
                await using var ctx = _contextFactory.CreateContext(enableTracking: true);

                var a = await ctx.Set<EFClientStatistics>()
                    .Where(s => s.ServerId == serverId)
                    //.Where(s=> s.ClientId == 216105)
                    .Where(s => s.Skill > 0)
                    .Where(s => s.EloRating > 0)
                    .Where(s => s.Client.Level != EFClient.Permission.Banned)
                    .Where(s => s.TimePlayed >= 3600 * 3)
                    .Where(s => s.UpdatedAt >= Extensions.FifteenDaysAgo())
                    .ToListAsync();

                var b = a.Distinct();

                foreach (var item in b)
                {
                    await Plugin.Manager.UpdateHistoricalRanking(item.ClientId, item, item.ServerId);
                    //item.ZScore = await GetZScoreForServer(serverId, item.Performance);
                   //item.UpdatedAt = DateTime.UtcNow;
                }

                await ctx.SaveChangesAsync();
            }*/
        }

        private async Task LoadServers()
        {
            if (_serverIds.Count == 0)
            {
                await using var context = contextFactory.CreateContext(false);
                _serverIds.AddRange(await context.Servers
                    .Where(s => s.EndPoint != null && s.HostName != null)
                    .Select(s => new Tuple<long, string>(s.ServerId, s.PerformanceBucket == null ? null : s.PerformanceBucket.Code))
                    .ToListAsync());
            }
        }

        /// <summary>
        /// Converts a raw performance value into a Z-score using the cached log-normal distribution
        /// for the given server or bucket. Looks up server-specific params first, falls back to bucket-level.
        /// Returns 0 if no distribution data is available (e.g. too few players).
        /// </summary>
        public async Task<double> GetZScoreForServerOrBucket(double value, long? serverId = null,
            string performanceBucket = null)
        {
            if (serverId is null && performanceBucket is null)
            {
                return 0.0;
            }

            var serverParams = await distributionCache.GetCacheItem(DistributionCacheKey, new CancellationToken());
            Extensions.LogParams sdParams = null;

            if (serverId is not null && serverParams.TryGetValue(serverId.ToString(), out var sdParams1))
            {
                sdParams = sdParams1;
            }

            else if (performanceBucket is not null && serverParams.TryGetValue(performanceBucket, out var sdParams2))
            {
                sdParams = sdParams2;
            }

            if (sdParams is null || sdParams.Sigma == 0)
            {
                return 0.0;
            }

            value = Math.Max(1, value);

            var zScore = (Math.Log(value) - sdParams.Mean) / sdParams.Sigma;
            return zScore;
        }

        /// <summary>
        /// Normalizes a Z-score into a 0–1 performance rating by dividing by the max Z-score
        /// in the bucket. Returns null if no max is available (empty bucket or cache miss).
        /// </summary>
        public async Task<double?> GetRatingForZScore(double? value, string performanceBucket)
        {
            try
            {
                // Normalise so null/empty/"default" all hit the same cache entry as
                // the seed loop wrote. Pre-fix this used `?? string.Empty` while the
                // distribution-cache loop used `?? "null"` — three different fallbacks
                // for the same logical bucket meant a null caller never hit any seeded
                // entry and the writer aborted via the null-rating branch.
                var key = PerformanceBucketCodes.Normalize(performanceBucket);
                var maxZScore = await maxZScoreCache.GetCacheItem(MaxZScoreCacheKey, [key]);
                return maxZScore == 0 ? null : value.GetRatingForZScore(maxZScore);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }
    }
}
