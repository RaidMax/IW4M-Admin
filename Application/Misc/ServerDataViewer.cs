﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models.Client;
using Data.Models.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Misc
{
    /// <inheritdoc/>
    public class ServerDataViewer : IServerDataViewer
    {
        private readonly ILogger _logger;
        private readonly IDataValueCache<EFServerSnapshot, int> _snapshotCache;
        private readonly IDataValueCache<EFClient, (int, int)> _serverStatsCache;
        private readonly IDataValueCache<EFServerSnapshot, List<ClientHistoryInfo>> _clientHistoryCache;

        private readonly TimeSpan? _cacheTimeSpan =
            Utilities.IsDevelopment ? TimeSpan.FromSeconds(1) : (TimeSpan?) TimeSpan.FromMinutes(1);

        public ServerDataViewer(ILogger<ServerDataViewer> logger, IDataValueCache<EFServerSnapshot, int> snapshotCache,
            IDataValueCache<EFClient, (int, int)> serverStatsCache,
            IDataValueCache<EFServerSnapshot, List<ClientHistoryInfo>> clientHistoryCache)
        {
            _logger = logger;
            _snapshotCache = snapshotCache;
            _serverStatsCache = serverStatsCache;
            _clientHistoryCache = clientHistoryCache;
        }

        public async Task<int> MaxConcurrentClientsAsync(long? serverId = null, TimeSpan? overPeriod = null,
            CancellationToken token = default)
        {
            _snapshotCache.SetCacheItem(async (snapshots, cancellationToken) =>
            {
                var oldestEntry = overPeriod.HasValue
                    ? DateTime.UtcNow - overPeriod.Value
                    : DateTime.UtcNow.AddDays(-1);
                var maxClients = 0;

                if (serverId != null)
                {
                    maxClients = await snapshots.Where(snapshot => snapshot.ServerId == serverId)
                        .Where(snapshot => snapshot.CapturedAt >= oldestEntry)
                        .MaxAsync(snapshot => (int?) snapshot.ClientCount, cancellationToken) ?? 0;
                }

                else
                {
                    maxClients = await snapshots.Where(snapshot => snapshot.CapturedAt >= oldestEntry)
                        .GroupBy(snapshot => snapshot.PeriodBlock)
                        .Select(grp => grp.Sum(snapshot => (int?) snapshot.ClientCount))
                        .MaxAsync(cancellationToken) ?? 0;
                }

                _logger.LogDebug("Max concurrent clients since {Start} is {Clients}", oldestEntry, maxClients);

                return maxClients;
            }, nameof(MaxConcurrentClientsAsync), _cacheTimeSpan);

            try
            {
                return await _snapshotCache.GetCacheItem(nameof(MaxConcurrentClientsAsync), token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not retrieve data for {Name}", nameof(MaxConcurrentClientsAsync));
                return 0;
            }
        }

        public async Task<(int, int)> ClientCountsAsync(TimeSpan? overPeriod = null, CancellationToken token = default)
        {
            _serverStatsCache.SetCacheItem(async (set, cancellationToken) =>
            {
                var count = await set.CountAsync(cancellationToken);
                var startOfPeriod =
                    DateTime.UtcNow.AddHours(-overPeriod?.TotalHours ?? -24);
                var recentCount = await set.CountAsync(client => client.LastConnection >= startOfPeriod,
                    cancellationToken);

                return (count, recentCount);
            }, nameof(_serverStatsCache), _cacheTimeSpan);

            try
            {
                return await _serverStatsCache.GetCacheItem(nameof(_serverStatsCache), token);
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
                            snapshot.ClientCount
                        })
                    .OrderBy(snapshot => snapshot.CapturedAt)
                    .ToListAsync(cancellationToken);

                return history.GroupBy(snapshot => snapshot.ServerId).Select(byServer => new ClientHistoryInfo
                {
                    ServerId = byServer.Key,
                    ClientCounts = byServer.Select(snapshot => new ClientCountSnapshot()
                        {Time = snapshot.CapturedAt, ClientCount = snapshot.ClientCount}).ToList()
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
    }
}