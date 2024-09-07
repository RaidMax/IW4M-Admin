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
    public class ServerDistributionCalculator : IServerDistributionCalculator
    {
        private readonly IDatabaseContextFactory _contextFactory;

        private readonly IDataValueCache<EFClientStatistics, Dictionary<string, Extensions.LogParams>>
            _distributionCache;

        private readonly IDataValueCache<EFClientStatistics, double> _maxZScoreCache;

        private readonly StatsConfiguration _configuration;
        private readonly ApplicationConfiguration _appConfig;
        private readonly List<Tuple<long, string>> _serverIds = [];

        private const string DistributionCacheKey = nameof(DistributionCacheKey);
        private const string MaxZScoreCacheKey = nameof(MaxZScoreCacheKey);

        public ServerDistributionCalculator(IDatabaseContextFactory contextFactory,
            IDataValueCache<EFClientStatistics, Dictionary<string, Extensions.LogParams>> distributionCache,
            IDataValueCache<EFClientStatistics, double> maxZScoreCache,
            StatsConfiguration config, ApplicationConfiguration appConfig)
        {
            _contextFactory = contextFactory;
            _distributionCache = distributionCache;
            _maxZScoreCache = maxZScoreCache;
            _configuration = config;
            _appConfig = appConfig;
        }

        public async Task Initialize()
        {
            await LoadServers();

            _distributionCache.SetCacheItem(async (set, token) =>
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
                        _configuration.PerformanceBuckets.FirstOrDefault(bucket =>
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

                foreach (var performanceBucket in _appConfig.Servers.Select(server => server.PerformanceBucketCode).Distinct())
                {
                    // TODO: ?
                    var performanceBucketCode = performanceBucket ?? "null";

                    var bucketConfig =
                        _configuration.PerformanceBuckets.FirstOrDefault(bucket =>
                            bucket.Code == performanceBucketCode) ?? new PerformanceBucketConfiguration();

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

            foreach (var performanceBucket in _appConfig.Servers.Select(s => s.PerformanceBucketCode).Distinct())
            {
                _maxZScoreCache.SetCacheItem(async (set, ids, token) =>
                    {
                        var validPlayTime = _configuration.TopPlayersMinPlayTime;
                        var oldestStat = DateTime.UtcNow - Extensions.FifteenDaysAgo();
                        var localPerformanceBucket = (string)ids.FirstOrDefault();

                        if (!string.IsNullOrEmpty(localPerformanceBucket))
                        {
                            var bucketConfig =
                                _configuration.PerformanceBuckets.FirstOrDefault(cfg =>
                                    cfg.Code == localPerformanceBucket) ?? new PerformanceBucketConfiguration();

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

                await _maxZScoreCache.GetCacheItem(MaxZScoreCacheKey, new[] { performanceBucket });
            }

            await _distributionCache.GetCacheItem(DistributionCacheKey, new CancellationToken());

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
                await using var context = _contextFactory.CreateContext(false);
                _serverIds.AddRange(await context.Servers
                    .Where(s => s.EndPoint != null && s.HostName != null)
                    .Select(s => new Tuple<long, string>(s.ServerId, s.PerformanceBucket.Code))
                    .ToListAsync());
            }
        }

        public async Task<double> GetZScoreForServerOrBucket(double value, long? serverId = null,
            string performanceBucket = null)
        {
            if (serverId is null && performanceBucket is null)
            {
                return 0.0;
            }

            var serverParams = await _distributionCache.GetCacheItem(DistributionCacheKey, new CancellationToken());
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

        public async Task<double?> GetRatingForZScore(double? value, string performanceBucket)
        {
            var maxZScore = await _maxZScoreCache.GetCacheItem(MaxZScoreCacheKey, new[] { performanceBucket });
            return maxZScore == 0 ? null : value.GetRatingForZScore(maxZScore);
        }
    }
}
