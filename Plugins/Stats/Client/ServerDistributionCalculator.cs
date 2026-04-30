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

                // DB stores PerformanceBucket.Code lowercased (the writer in
                // IW4MServer normalizes via ToLowerInvariant on insert), but the
                // server config may have mixed case ("Zombies"). Normalize the
                // iterator here so the LINQ comparison against the DB column at
                // line `perf.Server.PerformanceBucket.Code == performanceBucketCode`
                // doesn't silently miss every row in PostgreSQL (case-sensitive
                // string equality). Same fix applies to the maxZScore seed below.
                foreach (var performanceBucket in appConfig.Servers
                             .Select(server => server.PerformanceBucketCode?.ToLowerInvariant())
                             .Distinct())
                {
                    // TODO: ?
                    var performanceBucketCode = performanceBucket ?? "null";

                    var bucketConfig =
                        config.PerformanceBuckets.FirstOrDefault(bucket =>
                            string.Equals(bucket.Code, performanceBucketCode, StringComparison.OrdinalIgnoreCase))
                        ?? new PerformanceBucketConfiguration();

                    var oldestPerf = DateTime.UtcNow - bucketConfig.RankingExpiration;
                    var performances = await iqPerformances
                        .Where(perf => perf.Server.PerformanceBucket.Code == performanceBucketCode)
                        .Where(perf => perf.TimePlayed >= bucketConfig.ClientMinPlayTime.TotalSeconds)
                        .Where(perf => perf.UpdatedAt >= oldestPerf)
                        .Where(perf => perf.Skill < 999999)
                        .Select(s => s.EloRating * 1 / 3.0 + s.Skill * 2 / 3.0)
                        .ToListAsync(token);
                    var distributionParams = performances.GenerateDistributionParameters();
                    distributions.Add(performanceBucketCode, distributionParams);
                }

                return distributions;
            }, DistributionCacheKey, Utilities.IsDevelopment ? TimeSpan.FromMinutes(1) : TimeSpan.FromHours(1));

            // Same case-mismatch defense as the distribution-cache loop above —
            // see comment there. This seeds the maxZScore cache that
            // GetRatingForZScore reads, which is what gates the aggregate-ranking
            // writer at StatManager.UpdateAggregateForServerOrBucket. Without
            // normalization, the DB filter returns no rows, max becomes 0, and
            // every aggregate-ranking insert silently early-returns — leaving
            // the per-bucket leaderboard empty.
            foreach (var performanceBucket in appConfig.Servers
                         .Select(s => s.PerformanceBucketCode?.ToLowerInvariant() ?? string.Empty)
                         .Distinct())
            {
                maxZScoreCache.SetCacheItem(async (set, ids, token) =>
                    {
                        var validPlayTime = config.TopPlayersMinPlayTime;
                        var oldestStat = DateTime.UtcNow - Extensions.FifteenDaysAgo();
                        var localPerformanceBucket = (string)ids.FirstOrDefault();

                        if (!string.IsNullOrEmpty(localPerformanceBucket))
                        {
                            var bucketConfig =
                                config.PerformanceBuckets.FirstOrDefault(cfg =>
                                    string.Equals(cfg.Code, localPerformanceBucket, StringComparison.OrdinalIgnoreCase))
                                ?? new PerformanceBucketConfiguration();

                            validPlayTime = (int)bucketConfig.ClientMinPlayTime.TotalSeconds;
                            oldestStat = bucketConfig.RankingExpiration;
                        }

                        var zScore = await set
                            .Where(AdvancedClientStatsResourceQueryHelper.GetRankingFunc(validPlayTime, oldestStat))
                            .Where(s => s.Skill > 0)
                            .Where(s => s.EloRating >= 0)
                            .Where(stat => localPerformanceBucket == stat.Server.PerformanceBucket.Code)
                            .GroupBy(stat => stat.ClientId)
                            .Select(group =>
                                group.Sum(stat => stat.ZScore * stat.TimePlayed) / group.Sum(stat => stat.TimePlayed))
                            .MaxAsync(avgZScore => (double?)avgZScore, token);

                        return zScore ?? 0;
                    }, MaxZScoreCacheKey, new[] { performanceBucket },
                    Utilities.IsDevelopment ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(30));

                await maxZScoreCache.GetCacheItem(MaxZScoreCacheKey, new[] { performanceBucket });
            }

            await distributionCache.GetCacheItem(DistributionCacheKey, new CancellationToken());

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
                var maxZScore = await maxZScoreCache.GetCacheItem(MaxZScoreCacheKey, new[] { performanceBucket ?? string.Empty });
                return maxZScore == 0 ? null : value.GetRatingForZScore(maxZScore);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }
    }
}
