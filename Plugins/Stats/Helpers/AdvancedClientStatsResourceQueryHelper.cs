using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Data.Models.Client;
using Data.Models.Client.Stats;
using IW4MAdmin.Plugins.Stats;
using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using Stats.Dtos;

namespace Stats.Helpers
{
    public class AdvancedClientStatsResourceQueryHelper(
        ILogger<AdvancedClientStatsResourceQueryHelper> logger,
        IDatabaseContextFactory contextFactory,
        IServerDataViewer serverDataViewer,
        StatManager statManager
    )
        : IResourceQueryHelper<StatsInfoRequest, AdvancedStatsInfo>,
            IResourceQueryHelper<ClientRankingInfoRequest, ClientRankingInfo>
    {
        public async Task<ResourceQueryHelperResult<AdvancedStatsInfo>> QueryResource(StatsInfoRequest query)
        {
            await using var context = contextFactory.CreateContext(enableTracking: false);

            long? serverId = null;

            if (!string.IsNullOrEmpty(query.ServerEndpoint))
            {
                serverId = (await context.Servers
                        .Select(server => new { server.EndPoint, server.Id })
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
                .Where(r => r.PerformanceBucket == query.PerformanceBucket)
                .OrderByDescending(r => r.CreatedDateTime)
                .Take(250)
                .ToListAsync();

            var rankingInfo = (await QueryResource(new ClientRankingInfoRequest
            {
                ClientId = query.ClientId,
                ServerEndpoint = query.ServerEndpoint,
                PerformanceBucket = query.PerformanceBucket
            })).Results.First();

            var mostRecentRanking = ratings.FirstOrDefault(ranking => ranking.Newest);
            var ranking = mostRecentRanking?.Ranking + 1;

            // get stat for server, or all if no serverId
            var legacyStats = await context.Set<EFClientStatistics>()
                .Where(stat => stat.ClientId == query.ClientId)
                .Where(stat => serverId == null || stat.ServerId == serverId)
                .ToListAsync();

            var bucketConfig = await statManager.GetBucketConfig(serverId);
            
            if (mostRecentRanking != null && mostRecentRanking.CreatedDateTime < DateTime.UtcNow - bucketConfig.RankingExpiration)
            {
                ranking = 0;
            }

            if (clientInfo.Level == EFClient.Permission.Banned)
            {
                ranking = null;
            }

            var hitInfo = new AdvancedStatsInfo
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
                Servers = Plugin.ServerManager.GetServers()
                    .Select(server => new ServerInfo
                    {
                        Name = server.Hostname, IPAddress = server.ListenAddress, Port = server.ListenPort,
                        Game = (Reference.Game)server.GameName
                    })
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
                TotalRankedClients = rankingInfo.TotalRankedClients,
                PerformanceBucket = rankingInfo.PerformanceBucket
            };

            return new ResourceQueryHelperResult<AdvancedStatsInfo>
            {
                Results = new[] { hitInfo }
            };
        }

        public static Expression<Func<EFClientStatistics, bool>> GetRankingFunc(int minPlayTime, TimeSpan expiration,
            double? zScore = null,
            long? serverId = null)
        {
            var oldestStat = DateTime.UtcNow.Subtract(expiration);
            return stats => (serverId == null || stats.ServerId == serverId) &&
                            stats.UpdatedAt >= oldestStat &&
                            stats.Client.Level != EFClient.Permission.Banned &&
                            stats.TimePlayed >= minPlayTime
                            && (zScore == null || stats.ZScore > zScore);
        }

        public async Task<ResourceQueryHelperResult<ClientRankingInfo>> QueryResource(ClientRankingInfoRequest query)
        {
            await using var context = contextFactory.CreateContext(enableTracking: false);

            long? serverId = null;

            if (!string.IsNullOrEmpty(query.ServerEndpoint))
            {
                serverId = Plugin.ServerManager.Servers.FirstOrDefault(server => server.Id == query.ServerEndpoint)
                    ?.LegacyDatabaseId;
            }

            var currentRanking = 0;
            int totalRankedClients;
            string performanceBucket;

            if (string.IsNullOrEmpty(query.PerformanceBucket) && serverId is null)
            {
                var maxPerformance = await context.Set<EFClientRankingHistory>()
                    .Where(r => r.ClientId == query.ClientId)
                    .Where(r => r.Ranking != null)
                    .Where(r => r.ServerId == serverId)
                    .Where(rating => rating.Newest)
                    .GroupBy(rating => rating.PerformanceBucket)
                    .Select(grp => new { grp.Key, PerformanceMetric = grp.Max(rating => rating.Ranking) })
                    .Where(grp => grp.PerformanceMetric != null)
                    .FirstOrDefaultAsync();

                if (maxPerformance is null)
                {
                    currentRanking = 0;
                    totalRankedClients = 0;
                    performanceBucket = null;
                }
                else
                {
                    currentRanking =
                        await statManager.GetClientOverallRanking(query.ClientId!.Value, null, maxPerformance.Key);
                    totalRankedClients = await serverDataViewer.RankedClientsCountAsync(null, maxPerformance.Key);
                    performanceBucket = maxPerformance.Key;
                }
            }
            else
            {
                performanceBucket = query.PerformanceBucket;
                currentRanking =
                    await statManager.GetClientOverallRanking(query.ClientId!.Value, serverId, performanceBucket);
                totalRankedClients = await serverDataViewer.RankedClientsCountAsync(serverId, performanceBucket);
            }

            return new ResourceQueryHelperResult<ClientRankingInfo>
            {
                Results = [new ClientRankingInfo(currentRanking, totalRankedClients, performanceBucket)]
            };
        }
    }
}
