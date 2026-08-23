using SharedLibraryCore;
using Data.Models;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Database.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.IO
{
    public class GameLogEventDetection(
        ILogger<GameLogEventDetection> logger,
        IW4MServer server,
        Uri[] gameLogUris,
        IGameLogReaderFactory gameLogReaderFactory)
        : IDisposable
    {
        private long _previousFileSize;
        private readonly Server _server = server;

        private readonly IGameLogReader _reader =
            gameLogReaderFactory.CreateGameLogReader(gameLogUris, server.EventParser);

        private readonly bool _ignoreBots =
            server.Manager.GetApplicationSettings().Configuration()?.IgnoreBots ?? false;

        private readonly ILogger _logger = logger;
        private readonly CancellationTokenSource _cts = new();
        private bool _disposed;

        public async Task PollForChanges()
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cts.Token, _server.Manager.CancellationToken);

            while (!linkedCts.Token.IsCancellationRequested)
            {
                if (_server.IsInitialized)
                {
                    try
                    {
                        await UpdateLogEvents();
                    }

                    catch (Exception e)
                    {
                        using (LogContext.PushProperty("Server", _server.ToString()))
                        {
                            _logger.LogError(e, "Failed to update log event for {endpoint}", _server.EndPoint);
                        }
                    }
                }

                try
                {
                    await Task.Delay(_reader.UpdateInterval, linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogDebug("Stopped polling for changes");
        }

        private async Task UpdateLogEvents()
        {
            var fileSize = _reader.Length;

            if (_previousFileSize == 0)
            {
                _previousFileSize = fileSize;
            }

            var fileDiff = fileSize - _previousFileSize;

            // this makes the http log get pulled
            if (fileDiff < 1 && fileSize != -1)
            {
                _previousFileSize = fileSize;
                return;
            }

            var events = await _reader.ReadEventsFromLog(fileDiff, _previousFileSize, _server);

            foreach (var gameEvent in events)
            {
                try
                {
                    gameEvent.Owner = _server;

                    // we don't want to add the event if ignoreBots is on and the event comes from a bot
                    if (!_ignoreBots || (_ignoreBots &&
                                         !((gameEvent.Origin?.IsBot ?? false) || (gameEvent.Target?.IsBot ?? false))))
                    {
                        if ((gameEvent.RequiredEntity & GameEvent.EventRequiredEntity.Origin) ==
                            GameEvent.EventRequiredEntity.Origin)
                        {
                            var originName = (gameEvent as ClientGameEvent)?.ClientName;
                            if (gameEvent.Origin.NetworkId != Utilities.WORLD_ID ||
                                (_server.GameCode == Reference.Game.D7D && !string.IsNullOrWhiteSpace(originName)))
                            {
                                gameEvent.Origin = ResolveClient(gameEvent.Origin.NetworkId, originName,
                                    resolveNameOnlyClient: _server.GameCode == Reference.Game.D7D,
                                    allowWorldFallback: true) ?? gameEvent.Origin;
                            }
                        }

                        if ((gameEvent.RequiredEntity & GameEvent.EventRequiredEntity.Target) ==
                            GameEvent.EventRequiredEntity.Target)
                        {
                            var targetName = (gameEvent as ClientDamageEvent)?.VictimClientName;
                            gameEvent.Target = ResolveClient(gameEvent.Target.NetworkId, targetName,
                                resolveNameOnlyClient: _server.GameCode == Reference.Game.D7D);
                        }

                        if (gameEvent.Origin != null)
                        {
                            gameEvent.Origin.CurrentServer = _server;
                        }

                        if (gameEvent.Target != null)
                        {
                            gameEvent.Target.CurrentServer = _server;
                        }

                        _server.Manager.AddEvent(gameEvent);
                    }
                }

                catch (InvalidOperationException)
                {
                    if (_ignoreBots)
                    {
                        continue;
                    }

                    using (LogContext.PushProperty("Server", _server.ToString()))
                    {
                        _logger.LogError("Could not find client in client list when parsing event line {data}",
                            gameEvent.Data);
                    }
                }
            }

            _previousFileSize = fileSize;
        }

        private EFClient ResolveClient(long networkId, string clientName, bool resolveNameOnlyClient = false,
            bool allowWorldFallback = false)
        {
            var clients = _server.GetClientsAsList();
            if (networkId != Utilities.WORLD_ID || !resolveNameOnlyClient)
            {
                return clients.First(client => client.NetworkId == networkId);
            }

            var normalizedName = clientName?.Trim().StripColors();
            var matches = clients.Where(client => string.Equals(client.Name?.StripColors(), normalizedName,
                StringComparison.OrdinalIgnoreCase)).ToList();

            return allowWorldFallback && matches.Count != 1 ? null : matches.Single();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _cts.Cancel();
            _cts.Dispose();
            _disposed = true;
        }
    }
}
