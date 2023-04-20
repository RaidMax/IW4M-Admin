using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Data.Models.Client;
using Data.Models.Client.Stats;
using Data.Models.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Misc
{
    /// <inheritdoc/>
    public class ServerDataViewer : IServerDataViewer
    {
        private readonly ILogger _logger;
        private readonly IDataValueCache<EFServerSnapshot, (int?, DateTime?)> _snapshotCache;
        private readonly IDataValueCache<EFClient, (int, int)> _serverStatsCache;
        private readonly IDataValueCache<EFServerSnapshot, List<ClientHistoryInfo>> _clientHistoryCache;
        private readonly IDataValueCache<EFClientRankingHistory, int> _rankedClientsCache;

        private readonly TimeSpan? _cacheTimeSpan =
            Utilities.IsDevelopment ? TimeSpan.FromSeconds(30) : (TimeSpan?) TimeSpan.FromMinutes(10);

        public ServerDataViewer(ILogger<ServerDataViewer> logger, IDataValueCache<EFServerSnapshot, (int?, DateTime?)> snapshotCache,
            IDataValueCache<EFClient, (int, int)> serverStatsCache,
            IDataValueCache<EFServerSnapshot, List<ClientHistoryInfo>> clientHistoryCache, IDataValueCache<EFClientRankingHistory, int> rankedClientsCache)
        {
            _logger = logger;
            _snapshotCache = snapshotCache;
            _serverStatsCache = serverStatsCache;
            _clientHistoryCache = clientHistoryCache;
            _rankedClientsCache = rankedClientsCache;
        }

        public async Task<(int?, DateTime?)> 
            MaxConcurrentClientsAsync(long? serverId = null, Reference.Game? gameCode = null, TimeSpan? overPeriod = null,
            CancellationToken token = default)
        {
            _snapshotCache.SetCacheItem(async (snapshots, ids, cancellationToken) =>
            {
                Reference.Game? game = null;
                long? id = null;

                if (ids.Any())
                {
                    game = (Reference.Game?)ids.First();
                    id = (long?)ids.Last();
                }

                var oldestEntry = overPeriod.HasValue
                    ? DateTime.UtcNow - overPeriod.Value
                    : DateTime.UtcNow.AddDays(-1);

                int? maxClients;
                DateTime? maxClientsTime;

                if (id != null)
                {
                    var clients = await snapshots.Where(snapshot => snapshot.ServerId == id)
                        .Where(snapshot => game == null || snapshot.Server.GameName == game)
                        .Where(snapshot => snapshot.CapturedAt >= oldestEntry)
                        .OrderByDescending(snapshot => snapshot.ClientCount)
                        .Select(snapshot => new
                        {
                            snapshot.ClientCount,
                            snapshot.CapturedAt
                        })
                        .FirstOrDefaultAsync(cancellationToken);

                    maxClients = clients?.ClientCount;
                    maxClientsTime = clients?.CapturedAt;
                }

                else
                {
                    var clients = await snapshots.Where(snapshot => snapshot.CapturedAt >= oldestEntry)
                        .Where(snapshot => game == null || snapshot.Server.GameName == game)
                        .GroupBy(snapshot => snapshot.PeriodBlock)
                        .Select(grp => new
                        {
                            ClientCount = grp.Sum(snapshot => (int?)snapshot.ClientCount),
                            Time = grp.Max(snapshot => (DateTime?)snapshot.CapturedAt)
                        })
                        .OrderByDescending(snapshot => snapshot.ClientCount)
                        .FirstOrDefaultAsync(cancellationToken);

                    maxClients = clients?.ClientCount;
                    maxClientsTime = clients?.Time;
                }

                _logger.LogDebug("Max concurrent clients since {Start} is {Clients}", oldestEntry, maxClients);

                return (maxClients, maxClientsTime);
            }, nameof(MaxConcurrentClientsAsync), new object[] { gameCode, serverId }, _cacheTimeSpan, true);

            try
            {
                return await _snapshotCache.GetCacheItem(nameof(MaxConcurrentClientsAsync),
                    new object[] { gameCode, serverId }, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not retrieve data for {Name}", nameof(MaxConcurrentClientsAsync));
                return (null, null);
            }
        }

        public async Task<(int, int)> ClientCountsAsync(TimeSpan? overPeriod = null, Reference.Game? gameCode = null, CancellationToken token = default)
        {
            _serverStatsCache.SetCacheItem(async (set, ids, cancellationToken) =>
            {
                Reference.Game? game = null;
                
                if (ids.Any())
                {
                    game = (Reference.Game?)ids.First();
                }
                
                var count = await set.CountAsync(item => game == null || item.GameName == game,
                    cancellationToken);
                var startOfPeriod =
                    DateTime.UtcNow.AddHours(-overPeriod?.TotalHours ?? -24);
                var recentCount = await set.CountAsync(client => (game == null || client.GameName == game) && client.LastConnection >= startOfPeriod,
                    cancellationToken);

                return (count, recentCount);
            }, nameof(_serverStatsCache), new object[] { gameCode }, _cacheTimeSpan, true);

            try
            {
                return await _serverStatsCache.GetCacheItem(nameof(_serverStatsCache), new object[] { gameCode }, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not retrieve data for {Name}", nameof(ClientCountsAsync));
                return (0, 0);
            }
        }

        public async Task<IEnumerable<ClientHistoryInfo>> ClientHistoryAsync(TimeSpan? overPeriod = null, CancellationToken token = default)
        {
            _clientHistoryCache.SetCacheItem(async (set, cancellationToken) =>
            {
                var oldestEntry = overPeriod.HasValue
                    ? DateTime.UtcNow - overPeriod.Value
                    : DateTime.UtcNow.AddHours(-12);

                var history = await set.Where(snapshot => snapshot.CapturedAt >= oldestEntry)
                    .Select(snapshot =>
                        new
                        {
                            snapshot.ServerId,
                            snapshot.CapturedAt,
                            snapshot.ClientCount,
                            snapshot.ConnectionInterrupted,
                            MapName = snapshot.Map.Name,
                        })
                    .OrderBy(snapshot => snapshot.CapturedAt)
                    .ToListAsync(cancellationToken);

                return history.GroupBy(snapshot => snapshot.ServerId).Select(byServer => new ClientHistoryInfo
                {
                    ServerId = byServer.Key,
                    ClientCounts = byServer.Select(snapshot => new ClientCountSnapshot
                        { Time = snapshot.CapturedAt, ClientCount = snapshot.ClientCount, ConnectionInterrupted = snapshot.ConnectionInterrupted ?? false, Map = snapshot.MapName}).ToList()
                }).ToList();
            }, nameof(_clientHistoryCache), TimeSpan.MaxValue);

            try
            {
                return await _clientHistoryCache.GetCacheItem(nameof(_clientHistoryCache), token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not retrieve data for {Name}", nameof(ClientHistoryAsync));
                return Enumerable.Empty<ClientHistoryInfo>();
            }
        }

        public async Task<int> RankedClientsCountAsync(long? serverId = null, CancellationToken token = default)
        {
            _rankedClientsCache.SetCacheItem((set, ids, cancellationToken) =>
            {
                long? id = null;
                
                if (ids.Any())
                {
                    id = (long?)ids.First();
                }
                
                var fifteenDaysAgo = DateTime.UtcNow.AddDays(-15);
                return set
                    .Where(rating => rating.Newest)
                    .Where(rating => rating.ServerId == id)
                    .Where(rating => rating.CreatedDateTime >= fifteenDaysAgo)
                    .Where(rating => rating.Client.Level != EFClient.Permission.Banned)
                    .Where(rating => rating.Ranking != null)
                    .CountAsync(cancellationToken);
            }, nameof(_rankedClientsCache), new object[] { serverId }, _cacheTimeSpan);
            
            try
            {
                return await _rankedClientsCache.GetCacheItem(nameof(_rankedClientsCache), new object[] { serverId }, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not retrieve data for {Name}", nameof(RankedClientsCountAsync));
                return 0;
            }
        }
    }
}
