using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IW4MAdmin.Application.IO
{
    class GameLogEventDetection
    {
        Server Server;
        long PreviousFileSize;
        IGameLogReader Reader;
        readonly string GameLogFile;

        class EventState
        {
            public ILogger Log { get; set; }
            public string ServerId { get; set; }
        }

        public GameLogEventDetection(Server server, string gameLogPath, Uri gameLogServerUri)
        {
            GameLogFile = gameLogPath;
            Reader = gameLogServerUri != null ? new GameLogReaderHttp(gameLogServerUri, gameLogPath, server.EventParser) : Reader = new GameLogReader(gameLogPath, server.EventParser); 
            Server = server;
        }

        public async Task PollForChanges()
        {
            while (!Server.Manager.ShutdownRequested())
            {
                if ((Server.Manager as ApplicationManager).IsInitialized)
                {
                    try
                    {
                        await UpdateLogEvents();
                    }

                    catch (Exception e)
                    {
                        Server.Logger.WriteWarning($"Failed to update log event for {Server.EndPoint}");
                        Server.Logger.WriteDebug($"Exception: {e.Message}");
                        Server.Logger.WriteDebug($"StackTrace: {e.StackTrace}");
                    }
                }
                Thread.Sleep(Reader.UpdateInterval);
            }
        }

        private async Task UpdateLogEvents()
        {
            long fileSize = Reader.Length;

            if (PreviousFileSize == 0)
                PreviousFileSize = fileSize;

            long fileDiff = fileSize - PreviousFileSize;

            // this makes the http log get pulled
            if (fileDiff < 1 && fileSize != -1)
                return;

            PreviousFileSize = fileSize;

            var events = await Reader.ReadEventsFromLog(Server, fileDiff, 0);

            foreach (var ev in events)
            {
                Server.Manager.GetEventHandler().AddEvent(ev);
                await ev.WaitAsync();
            }

            PreviousFileSize = fileSize;
        }
    }
}
