using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IW4MAdmin.Application.IO
{
    public class GameLogEventDetection
    {
        private long previousFileSize;
        private readonly Server _server;
        private readonly IGameLogReader _reader;
        private readonly bool _ignoreBots;

        class EventState
        {
            public ILogger Log { get; set; }
            public string ServerId { get; set; }
        }

        public GameLogEventDetection(Server server, string gameLogPath, Uri gameLogServerUri, IGameLogReader reader = null)
        {
            _reader = gameLogServerUri != null 
                ? reader ?? new GameLogReaderHttp(gameLogServerUri, gameLogPath, server.EventParser) 
                : reader ?? new GameLogReader(gameLogPath, server.EventParser);
            _server = server;
            _ignoreBots = server?.Manager.GetApplicationSettings().Configuration().IgnoreBots ?? false;
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
                        _server.Logger.WriteWarning($"Failed to update log event for {_server.EndPoint}");
                        _server.Logger.WriteDebug(e.GetExceptionInfo());
                    }
                }

                await Task.Delay(_reader.UpdateInterval, _server.Manager.CancellationToken);
            }

            _server.Logger.WriteDebug("Stopped polling for changes");
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

            var events = await _reader.ReadEventsFromLog(_server, fileDiff, previousFileSize);

            foreach (var gameEvent in events)
            {
                try
                {
#if DEBUG
                    _server.Logger.WriteVerbose(gameEvent.Data);
#endif
                    gameEvent.Owner = _server;

                    // we don't want to add the event if ignoreBots is on and the event comes from a bot
                    if (!_ignoreBots || (_ignoreBots && !((gameEvent.Origin?.IsBot ?? false) || (gameEvent.Target?.IsBot ?? false))))
                    {
                        if ((gameEvent.RequiredEntity & GameEvent.EventRequiredEntity.Origin) == GameEvent.EventRequiredEntity.Origin && gameEvent.Origin.NetworkId != 1)
                        {
                            gameEvent.Origin = _server.GetClientsAsList().First(_client => _client.NetworkId == gameEvent.Origin?.NetworkId);
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

                        _server.Manager.GetEventHandler().AddEvent(gameEvent);
                    }
                }

                catch (InvalidOperationException)
                {
                    if (!_ignoreBots)
                    {
                        _server.Logger.WriteWarning("Could not find client in client list when parsing event line");
                        _server.Logger.WriteDebug(gameEvent.Data);
                    }
                }
            }

            previousFileSize = fileSize;
        }
    }
}
