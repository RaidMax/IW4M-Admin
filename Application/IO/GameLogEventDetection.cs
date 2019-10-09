using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IW4MAdmin.Application.IO
{
    class GameLogEventDetection
    {
        private long previousFileSize;
        private readonly Server _server;
        private readonly IGameLogReader _reader;
        private readonly string _gameLogFile;

        class EventState
        {
            public ILogger Log { get; set; }
            public string ServerId { get; set; }
        }

        public GameLogEventDetection(Server server, string gameLogPath, Uri gameLogServerUri)
        {
            _gameLogFile = gameLogPath;
            _reader = gameLogServerUri != null ? new GameLogReaderHttp(gameLogServerUri, gameLogPath, server.EventParser) : _reader = new GameLogReader(gameLogPath, server.EventParser); 
            _server = server;
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

        private async Task UpdateLogEvents()
        {
            long fileSize = _reader.Length;

            if (previousFileSize == 0)
            {
                previousFileSize = fileSize;
            }

            long fileDiff = fileSize - previousFileSize;

            // this makes the http log get pulled
            if (fileDiff < 1 && fileSize != -1)
                return;

            var events = await _reader.ReadEventsFromLog(_server, fileDiff, previousFileSize);

            foreach (var ev in events)
            {
                _server.Manager.GetEventHandler().AddEvent(ev);
            }

            previousFileSize = fileSize;
        }
    }
}
