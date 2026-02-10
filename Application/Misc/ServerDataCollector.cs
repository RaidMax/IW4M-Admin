using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Data.Models.Client;
using Data.Models.Client.Stats.Reference;
using Data.Models.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Events.Management;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

namespace IW4MAdmin.Application.Misc;

/// <inheritdoc/>
public class ServerDataCollector : IServerDataCollector
{
    private readonly ILogger _logger;
    private readonly IManager _manager;
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly ApplicationConfiguration _appConfig;

    private bool _inProgress;
    private TimeSpan _period;
    private DateTime _lastActivityAggregation = DateTime.MinValue;
    private static readonly TimeSpan ActivityAggregationInterval = TimeSpan.FromMinutes(15);

    /// <summary>
    /// When closing a session on "reconnect" (Connect without prior Disconnect), cap the session length
    /// so reused ClientIds don't attribute a huge gap as one session. Tied to aggregation interval (2×) for leniency.
    /// </summary>
    private static readonly int MaxReconnectSessionMinutes = Math.Max(1, (int)ActivityAggregationInterval.TotalMinutes * 2);

    public ServerDataCollector(ILogger<ServerDataCollector> logger, ApplicationConfiguration appConfig,
        IManager manager, IDatabaseContextFactory contextFactory)
    {
        _logger = logger;
        _appConfig = appConfig;
        _manager = manager;
        _contextFactory = contextFactory;

        IManagementEventSubscriptions.ClientStateAuthorized += SaveConnectionInfo;
        IManagementEventSubscriptions.ClientStateDisposed += SaveConnectionInfo;
    }

    public async Task BeginCollectionAsync(TimeSpan? period = null, CancellationToken cancellationToken = default)
    {
        if (_inProgress)
        {
            throw new InvalidOperationException($"{nameof(ServerDataCollector)} is already collecting data");
        }

        _logger.LogDebug("Initializing data collection with {Name}", nameof(ServerDataCollector));
        _inProgress = true;
        _period = period ?? (Utilities.IsDevelopment
            ? TimeSpan.FromMinutes(1)
            : _appConfig.ServerDataCollectionInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_period, cancellationToken);
                _logger.LogDebug("{Name} is collecting server data", nameof(ServerDataCollector));

                var data = await BuildCollectionData(cancellationToken);
                await SaveData(data, cancellationToken);

                if (DateTime.UtcNow - _lastActivityAggregation >= ActivityAggregationInterval)
                {
                    try
                    {
                        await AggregateDailyActivityAsync(cancellationToken);
                        _lastActivityAggregation = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error running server activity aggregation in {Name}", nameof(ServerDataCollector));
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("Shutdown requested for {Name}", nameof(ServerDataCollector));
                return;
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error encountered collecting server data for {Name}",
                    nameof(ServerDataCollector));
            }
        }
    }

    private async Task<IEnumerable<EFServerSnapshot>> BuildCollectionData(CancellationToken token)
    {
        var data = await Task.WhenAll(_manager.GetServers()
            .Select(async server => new EFServerSnapshot
            {
                CapturedAt = DateTime.UtcNow,
                PeriodBlock = (int)(DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch).TotalMinutes,
                ServerId = await server.GetIdForServer(),
                MapId = await GetOrCreateMap(server.CurrentMap.Name, (Reference.Game)server.GameName, token),
                ClientCount = server.ClientNum,
                ConnectionInterrupted = server.Throttled || server.IsErrorState,
            }));

        return data;
    }

    private async Task<int> GetOrCreateMap(string mapName, Reference.Game game, CancellationToken token)
    {
        await using var context = _contextFactory.CreateContext();
        var existingMap =
            await context.Maps.FirstOrDefaultAsync(map => map.Name == mapName && map.Game == game, token);

        if (existingMap != null)
        {
            return existingMap.MapId;
        }

        var newMap = new EFMap
        {
            Name = mapName,
            Game = game
        };

        context.Maps.Add(newMap);
        await context.SaveChangesAsync(token);

        return newMap.MapId;
    }

    private async Task SaveData(IEnumerable<EFServerSnapshot> snapshots, CancellationToken token)
    {
        await using var context = _contextFactory.CreateContext();
        context.ServerSnapshots.AddRange(snapshots);
        await context.SaveChangesAsync(token);
    }

    private async Task SaveConnectionInfo(ClientStateEvent stateEvent, CancellationToken token)
    {
        await using var context = _contextFactory.CreateContext(enableTracking: false);
        context.ConnectionHistory.Add(new EFClientConnectionHistory
        {
            ClientId = stateEvent.Client.ClientId,
            ServerId = await stateEvent.Client.CurrentServer.GetIdForServer(),
            ConnectionType = stateEvent is ClientStateAuthorizeEvent
                ? Reference.ConnectionType.Connect
                : Reference.ConnectionType.Disconnect
        });

        await context.SaveChangesAsync(token);
    }

    private async Task AggregateDailyActivityAsync(CancellationToken token)
    {
        await using var context = _contextFactory.CreateContext();

        var endDate = DateTime.UtcNow;
        var startDate = endDate.Date.AddDays(-1);

        var pruneDate = endDate.AddDays(-31);
        try
        {
            var oldRecords = await context.GameStatistics
                .Where(x => x.Date < pruneDate)
                .ToListAsync(token);

            if (oldRecords.Count != 0)
            {
                context.GameStatistics.RemoveRange(oldRecords);
                await context.SaveChangesAsync(token);
                _logger.LogInformation("Pruned {Count} old activity records", oldRecords.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pruning old activity data");
        }

        // Aggregate by game (not per-server); EFServerStatistics covers per-server stats.
        var activeServers = _manager.GetServers().ToList();
        var gameDailyStats = new Dictionary<(Reference.Game?, DateTime), (long Minutes, int Connections, HashSet<int> Clients)>();

        for (var day = startDate; day <= endDate.Date; day = day.AddDays(1))
        {
            foreach (var server in activeServers)
            {
                var game = (Reference.Game?)server.GameName;
                var key = (game, day);
                if (!gameDailyStats.ContainsKey(key))
                    gameDailyStats[key] = (0, 0, []);
            }
        }

        foreach (var server in activeServers)
        {
            var serverId = await server.GetIdForServer();
            var game = (Reference.Game?)server.GameName;
            var currentlyConnectedClientIds = server.GetClientsAsList()
                .Where(c => !c.IsBot)
                .Select(c => c.ClientId)
                .ToHashSet();

            var events = await context.ConnectionHistory
                .Where(h => h.ServerId == serverId && h.CreatedDateTime >= startDate)
                .OrderBy(h => h.CreatedDateTime)
                .Select(h => new { h.ClientId, h.ConnectionType, h.CreatedDateTime })
                .ToListAsync(token);

            if (events.Count == 0) continue;

            var distinctClientIdsInEvents = events.Select(e => e.ClientId).Distinct().ToList();
            var clientRows = await context.Clients
                .Where(c => distinctClientIdsInEvents.Contains(c.ClientId))
                .Include(c => c.CurrentAlias)
                .Select(c => new { c.ClientId, c.NetworkId, Name = c.CurrentAlias != null ? c.CurrentAlias.Name : null })
                .ToListAsync(token);
            var botClientIds = clientRows
                .Where(c => c.NetworkId == (c.Name ?? "").StripColors().GenerateGuidFromString())
                .Select(c => c.ClientId)
                .ToHashSet();

            var dailyStats = new Dictionary<DateTime, (long Minutes, int Connections, HashSet<int> Clients)>();
            for (var day = startDate; day <= endDate.Date; day = day.AddDays(1))
                dailyStats[day] = (0, 0, []);

            var clientEvents = events.GroupBy(e => e.ClientId);

            foreach (var clientGroup in clientEvents)
            {
                if (botClientIds.Contains(clientGroup.Key)) continue;

                var sortedEvents = clientGroup.OrderBy(e => e.CreatedDateTime).ToList();
                DateTime? sessionStart = null;

                foreach (var evt in sortedEvents)
                {
                    var evtDate = evt.CreatedDateTime.Date;
                    if (!dailyStats.ContainsKey(evtDate)) continue;

                    switch (evt.ConnectionType)
                    {
                        case Reference.ConnectionType.Connect:
                        {
                            var stats = dailyStats[evtDate];
                            stats.Connections++;
                            stats.Clients.Add(clientGroup.Key);
                            dailyStats[evtDate] = stats;

                            if (sessionStart.HasValue)
                            {
                                var sessionEnd = evt.CreatedDateTime;
                                var sessionMinutes = (sessionEnd - sessionStart.Value).TotalMinutes;
                                if (sessionMinutes > MaxReconnectSessionMinutes)
                                    sessionEnd = sessionStart.Value.AddMinutes(MaxReconnectSessionMinutes);
                                AddPlaytime(dailyStats, sessionStart.Value, sessionEnd);
                            }
                            sessionStart = evt.CreatedDateTime;
                            break;
                        }
                        case Reference.ConnectionType.Disconnect when !sessionStart.HasValue:
                            continue;
                        case Reference.ConnectionType.Disconnect:
                            AddPlaytime(dailyStats, sessionStart.Value, evt.CreatedDateTime);
                            sessionStart = null;
                            break;
                    }
                }

                if (sessionStart.HasValue && currentlyConnectedClientIds.Contains(clientGroup.Key))
                    AddPlaytime(dailyStats, sessionStart.Value, endDate);
            }

            // Merge this server's daily stats into game-level aggregates
            foreach (var (date, value) in dailyStats)
            {
                var key = (game, date);
                if (!gameDailyStats.TryGetValue(key, out var existing))
                    gameDailyStats[key] = value;
                else
                {
                    var (min, conn, set) = existing;
                    foreach (var c in value.Clients) set.Add(c);
                    gameDailyStats[key] = (min + value.Minutes, conn + value.Connections, set);
                }
            }
        }

        // Persist one row per (Game, Date)
        foreach (var (key, value) in gameDailyStats)
        {
            var (game, date) = key;
            var (minutes, connections, uniqueClients) = value;

            var record = await context.GameStatistics
                .FirstOrDefaultAsync(x => x.GameName == (int?)game && x.Date == date, token);

            if (record == null)
            {
                record = new EFGameStatistic
                {
                    GameName = (int?)game,
                    Date = date
                };
                context.GameStatistics.Add(record);
            }

            record.PlayTimeMinutes = minutes;
            record.ConnectionCount = connections;
            record.UniqueClientCount = uniqueClients.Count;

            _logger.LogDebug(
                "Activity aggregate: Game={Game} Date={Date:yyyy-MM-dd} PlayTimeMinutes={PlayTimeMinutes} ConnectionCount={ConnectionCount} UniqueClientCount={UniqueClientCount}",
                game, date, minutes, connections, uniqueClients.Count);
        }

        await context.SaveChangesAsync(token);
    }

    private static void AddPlaytime(Dictionary<DateTime, (long Minutes, int Connections, HashSet<int> Clients)> dailyStats, DateTime start,
        DateTime end)
    {
        if ((end - start).TotalHours > 24)
        {
            return;
        }

        var current = start;
        while (current.Date < end.Date)
        {
            var endOfDay = current.Date.AddDays(1);
            var minutes = (long)(endOfDay - current).TotalMinutes;

            if (dailyStats.TryGetValue(current.Date, out var stats))
            {
                stats.Minutes += minutes;
                dailyStats[current.Date] = stats;
            }

            current = endOfDay;
        }

        var finalMinutes = (long)(end - current).TotalMinutes;
        if (finalMinutes <= 0 || !dailyStats.TryGetValue(current.Date, out var finalStats))
        {
            return;
        }

        finalStats.Minutes += finalMinutes;
        dailyStats[current.Date] = finalStats;
    }
}
