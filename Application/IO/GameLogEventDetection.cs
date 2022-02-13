using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.IO
{
    public class GameLogEventDetection
    {
        private long previousFileSize;
        private readonly Server _server;
        private readonly IGameLogReader _reader;
        private readonly bool _ignoreBots;
        private readonly ILogger _logger;

        public GameLogEventDetection(ILogger<GameLogEventDetection> logger, IW4MServer server, Uri[] gameLogUris, IGameLogReaderFactory gameLogReaderFactory)
        {
            _reader = gameLogReaderFactory.CreateGameLogReader(gameLogUris, server.EventParser);
            _server = server;
            _ignoreBots = server.Manager.GetApplicationSettings().Configuration()?.IgnoreBots ?? false;
            _logger = logger;
        }

        public async Task PollForChanges()
        {
            while (!_server.Manager.CancellationToken.IsCancellationRequested)
            {
                if (_server.IsInitialized)
                {
                    try
                    {
                        await UpdateLogEvents();
                    }

                    catch (Exception e)
                    {
                        using(LogContext.PushProperty("Server", _server.ToString()))
                        {
                            _logger.LogError(e, "Failed to update log event for {endpoint}", _server.EndPoint);
                        }
                    }
                }

                await Task.Delay(_reader.UpdateInterval, _server.Manager.CancellationToken);
            }

            _logger.LogDebug("Stopped polling for changes");
        }

        public async Task UpdateLogEvents()
        {
            long fileSize = _reader.Length;

            if (previousFileSize == 0)
            {
                previousFileSize = fileSize;
            }

            long fileDiff = fileSize - previousFileSize;

            // this makes the http log get pulled
            if (fileDiff < 1 && fileSize != -1)
            {
                previousFileSize = fileSize;
                return;
            }

            var events = await _reader.ReadEventsFromLog(fileDiff, previousFileSize, _server);

            foreach (var gameEvent in events)
            {
                try
                {
                    gameEvent.Owner = _server;

                    // we don't want to add the event if ignoreBots is on and the event comes from a bot
                    if (!_ignoreBots || (_ignoreBots && !((gameEvent.Origin?.IsBot ?? false) || (gameEvent.Target?.IsBot ?? false))))
                    {
                        if ((gameEvent.RequiredEntity & GameEvent.EventRequiredEntity.Origin) == GameEvent.EventRequiredEntity.Origin && gameEvent.Origin.NetworkId != Utilities.WORLD_ID)
                        {
                            gameEvent.Origin = _server.GetClientsAsList().First(_client => _client.NetworkId == gameEvent.Origin?.NetworkId);;
                        }

                        if ((gameEvent.RequiredEntity & GameEvent.EventRequiredEntity.Target) == GameEvent.EventRequiredEntity.Target)
                        {
                            gameEvent.Target = _server.GetClientsAsList().First(_client => _client.NetworkId == gameEvent.Target?.NetworkId);
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
                    
                    using(LogContext.PushProperty("Server", _server.ToString()))
                    {
                        _logger.LogError("Could not find client in client list when parsing event line {data}", gameEvent.Data);
                    }
                }
            }

            previousFileSize = fileSize;
        }
    }
}
