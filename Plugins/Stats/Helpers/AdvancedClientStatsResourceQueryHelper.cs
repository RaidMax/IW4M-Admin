using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Data.Models.Client;
using Data.Models.Client.Stats;
using IW4MAdmin.Plugins.Stats;
using IW4MAdmin.Plugins.Stats.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using Stats.Dtos;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Stats.Helpers
{
    public class AdvancedClientStatsResourceQueryHelper : IResourceQueryHelper<StatsInfoRequest, AdvancedStatsInfo>
    {
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly ILogger _logger;
        private readonly IManager _manager;

        public AdvancedClientStatsResourceQueryHelper(ILogger<AdvancedClientStatsResourceQueryHelper> logger,
            IDatabaseContextFactory contextFactory, IManager manager)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _manager = manager;
        }

        public async Task<ResourceQueryHelperResult<AdvancedStatsInfo>> QueryResource(StatsInfoRequest query)
        {
            await using var context = _contextFactory.CreateContext(enableTracking: false);

            long? serverId = null;

            if (!string.IsNullOrEmpty(query.ServerEndpoint))
            {
                serverId = (await context.Servers
                        .Select(server => new {server.EndPoint, server.Id})
                        .FirstOrDefaultAsync(server => server.EndPoint == query.ServerEndpoint))
                    ?.Id;
            }

            var clientInfo = await context.Clients.Select(client => new
            {
                client.ClientId,
                client.CurrentAlias.Name,
                client.Level,
                client.GameName
            }).FirstOrDefaultAsync(client => client.ClientId == query.ClientId);

            if (clientInfo == null)
            {
                return null;
            }

            // gets all the hit stats for the client
            var hitStats = await context.Set<EFClientHitStatistic>()
                .Include(stat => stat.HitLocation)
                .Include(stat => stat.MeansOfDeath)
                .Include(stat => stat.Weapon)
                .Include(stat => stat.WeaponAttachmentCombo)
                .ThenInclude(attachment => attachment.Attachment1)
                .Include(stat => stat.WeaponAttachmentCombo)
                .ThenInclude(attachment => attachment.Attachment2)
                .Include(stat => stat.WeaponAttachmentCombo)
                .ThenInclude(attachment => attachment.Attachment3)
                .Where(stat => stat.ClientId == query.ClientId)
                .Where(stat => stat.ServerId == serverId)
                .ToListAsync();

            var ratings = await context.Set<EFClientRankingHistory>()
                .Where(r => r.ClientId == clientInfo.ClientId)
                .Where(r => r.ServerId == serverId)
                .Where(r => r.Ranking != null)
                .OrderByDescending(r => r.UpdatedDateTime)
                .ToListAsync();

            var mostRecentRanking = ratings.FirstOrDefault(ranking => ranking.Newest);
            var ranking = mostRecentRanking?.Ranking + 1;

            // get stat for server, or all if no serverId
            var legacyStats = await context.Set<EFClientStatistics>()
                .Where(stat => stat.ClientId == query.ClientId)
                .Where(stat => serverId == null || stat.ServerId == serverId)
                .ToListAsync();

            if (mostRecentRanking != null && mostRecentRanking.CreatedDateTime < Extensions.FifteenDaysAgo())
            {
                ranking = 0;
            }

            if (clientInfo.Level == EFClient.Permission.Banned)
            {
                ranking = null;
            }

            var hitInfo = new AdvancedStatsInfo()
            {
                ServerId = serverId,
                Performance = mostRecentRanking?.PerformanceMetric,
                ZScore = mostRecentRanking?.ZScore,
                ServerEndpoint = query.ServerEndpoint,
                ClientName = clientInfo.Name,
                ClientId = clientInfo.ClientId,
                Level = clientInfo.Level,
                Rating = mostRecentRanking?.PerformanceMetric,
                All = hitStats,
                Servers = _manager.GetServers()
                    .Select(server => new ServerInfo
                        {Name = server.Hostname, IPAddress = server.IP, Port = server.Port, Game = (Reference.Game)server.GameName})
                    .Where(server => server.Game == clientInfo.GameName)
                    .ToList(),
                Aggregate = hitStats.FirstOrDefault(hit =>
                    hit.HitLocationId == null && hit.ServerId == serverId && hit.WeaponId == null &&
                    hit.MeansOfDeathId == null),
                ByHitLocation = hitStats
                    .Where(hit => hit.HitLocationId != null)
                    .Where(hit => hit.WeaponId == null)
                    .Where(hit => hit.WeaponAttachmentComboId == null)
                    .ToList(),
                ByWeapon = hitStats
                    .Where(hit => hit.HitLocationId == null)
                    .Where(hit => hit.WeaponId != null)
                    .ToList(),
                ByAttachmentCombo = hitStats
                    .Where(hit => hit.HitLocationId == null)
                    .Where(hit => hit.WeaponId != null)
                    .Where(hit => hit.WeaponAttachmentComboId != null)
                    .ToList(),
                Ratings = ratings,
                LegacyStats = legacyStats,
                Ranking = ranking,
            };

            // todo: when nothign found
            return new ResourceQueryHelperResult<AdvancedStatsInfo>()
            {
                Results = new[] {hitInfo}
            };
        }

        public static Expression<Func<EFClientStatistics, bool>> GetRankingFunc(int minPlayTime, double? zScore = null,
            long? serverId = null)
        {
            return (stats) => (serverId == null || stats.ServerId == serverId) &&
                              stats.UpdatedAt >= Extensions.FifteenDaysAgo() &&
                              stats.Client.Level != EFClient.Permission.Banned &&
                              stats.TimePlayed >= minPlayTime
                              && (zScore == null || stats.ZScore > zScore);
        }
    }
}
