using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models.Client;
using Data.Models.Client.Stats;
using IW4MAdmin.Plugins.Stats;
using IW4MAdmin.Plugins.Stats.Config;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using Stats.Client.Abstractions;
using Stats.Config;
using Stats.Helpers;

namespace Stats.Client
{
    public class ServerDistributionCalculator : IServerDistributionCalculator
    {
        private readonly IDatabaseContextFactory _contextFactory;

        private readonly IDataValueCache<EFClientStatistics, Dictionary<long, Extensions.LogParams>>
            _distributionCache;

        private readonly IDataValueCache<EFClientStatistics, double>
            _maxZScoreCache;

        private readonly IConfigurationHandler<StatsConfiguration> _configurationHandler;
        private readonly List<long> _serverIds = new List<long>();

        private const string DistributionCacheKey = nameof(DistributionCacheKey);
        private const string MaxZScoreCacheKey = nameof(MaxZScoreCacheKey);

        public ServerDistributionCalculator(IDatabaseContextFactory contextFactory,
            IDataValueCache<EFClientStatistics, Dictionary<long, Extensions.LogParams>> distributionCache,
            IDataValueCache<EFClientStatistics, double> maxZScoreCache,
            IConfigurationHandlerFactory configFactory)
        {
            _contextFactory = contextFactory;
            _distributionCache = distributionCache;
            _maxZScoreCache = maxZScoreCache;
            _configurationHandler = configFactory.GetConfigurationHandler<StatsConfiguration>("StatsPluginSettings");
        }

        public async Task Initialize()
        {
            await LoadServers();
            _distributionCache.SetCacheItem((async (set, token) =>
            {
                await _configurationHandler.BuildAsync();
                var validPlayTime = _configurationHandler.Configuration()?.TopPlayersMinPlayTime ?? 3600 * 3;

                var distributions = new Dictionary<long, Extensions.LogParams>();

                await LoadServers();

                foreach (var serverId in _serverIds)
                {
                    var performance = await set
                        .Where(s => s.ServerId == serverId)
                        .Where(s => s.Skill > 0)
                        .Where(s => s.EloRating > 0)
                        .Where(s => s.Client.Level != EFClient.Permission.Banned)
                        .Where(s => s.TimePlayed >= validPlayTime)
                        .Where(s => s.UpdatedAt >= Extensions.FifteenDaysAgo())
                        .Select(s => s.EloRating * 1 / 3.0 + s.Skill * 2 / 3.0).ToListAsync();
                    var distributionParams = performance.GenerateDistributionParameters();
                    distributions.Add(serverId, distributionParams);
                }

                return distributions;
            }), DistributionCacheKey, Utilities.IsDevelopment ? TimeSpan.FromMinutes(5) : TimeSpan.FromHours(1));

            _maxZScoreCache.SetCacheItem(async (set, token) =>
            {
                await _configurationHandler.BuildAsync();
                var validPlayTime = _configurationHandler.Configuration()?.TopPlayersMinPlayTime ?? 3600 * 3;

                var zScore = await set
                    .Where(AdvancedClientStatsResourceQueryHelper.GetRankingFunc(validPlayTime))
                    .Where(s => s.Skill > 0)
                    .Where(s => s.EloRating > 0)
                    .GroupBy(stat => stat.ClientId)
                    .Select(group =>
                        group.Sum(stat => stat.ZScore * stat.TimePlayed) / group.Sum(stat => stat.TimePlayed))
                    .MaxAsync(avgZScore => (double?) avgZScore, token);
                return zScore ?? 0;
            }, MaxZScoreCacheKey, Utilities.IsDevelopment ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(30));

            await _distributionCache.GetCacheItem(DistributionCacheKey);
            await _maxZScoreCache.GetCacheItem(MaxZScoreCacheKey);

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
                    .Select(s => s.ServerId)
                    .ToListAsync());
            }
        }

        public async Task<double> GetZScoreForServer(long serverId, double value)
        {
            var serverParams = await _distributionCache.GetCacheItem(DistributionCacheKey);
            if (!serverParams.ContainsKey(serverId))
            {
                return 0.0;
            }

            var sdParams = serverParams[serverId];
            if (sdParams.Sigma == 0)
            {
                return 0.0;
            }

            var zScore = (Math.Log(value) - sdParams.Mean) / sdParams.Sigma;
            return zScore;
        }

        public async Task<double?> GetRatingForZScore(double? value)
        {
            var maxZScore = await _maxZScoreCache.GetCacheItem(MaxZScoreCacheKey);
            return maxZScore == 0 ? null : value.GetRatingForZScore(maxZScore);
        }
    }
}
