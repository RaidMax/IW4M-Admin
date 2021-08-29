using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Data.Models.Client.Stats.Reference;
using Data.Models.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Misc
{
    /// <inheritdoc/>
    public class ServerDataCollector : IServerDataCollector
    {
        private readonly ILogger _logger;
        private readonly IManager _manager;
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly ApplicationConfiguration _appConfig;

        private bool _inProgress;
        private TimeSpan _period;

        public ServerDataCollector(ILogger<ServerDataCollector> logger, ApplicationConfiguration appConfig,
            IManager manager, IDatabaseContextFactory contextFactory)
        {
            _logger = logger;
            _appConfig = appConfig;
            _manager = manager;
            _contextFactory = contextFactory;
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
                    PeriodBlock = (int) (DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch).TotalMinutes,
                    ServerId = await server.GetIdForServer(),
                    MapId = await GetOrCreateMap(server.CurrentMap.Name, (Reference.Game) server.GameName, token),
                    ClientCount = server.ClientNum
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
    }
}