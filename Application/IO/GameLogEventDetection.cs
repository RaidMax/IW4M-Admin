using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
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

        public GameLogEventDetection(Server server, string gameLogPath, string gameLogName)
        {
            GameLogFile = gameLogPath;
            // todo: abtract this more
            if (gameLogPath.StartsWith("http"))
            {
                Reader = new GameLogReaderHttp(gameLogPath, server.EventParser);
            }
            else
            {
                Reader = new GameLogReader(gameLogPath, server.EventParser);
            }

            Server = server;
        }

        public void PollForChanges()
        {
            while (!Server.Manager.ShutdownRequested())
            {
                if ((Server.Manager as ApplicationManager).IsInitialized)
                {
                    try
                    {
                        UpdateLogEvents();
                    }

                    catch (Exception e)
                    {
                        Server.Logger.WriteWarning($"Failed to update log event for {Server.GetHashCode()}");
                        Server.Logger.WriteDebug($"Exception: {e.Message}");
                        Server.Logger.WriteDebug($"StackTrace: {e.StackTrace}");
                    }
                }
                Thread.Sleep(Reader.UpdateInterval);
            }
        }

        private void UpdateLogEvents()
        {
            long fileSize = Reader.Length;

            if (PreviousFileSize == 0)
                PreviousFileSize = fileSize;

            long fileDiff = fileSize - PreviousFileSize;

            if (fileDiff < 1)
                return;

            PreviousFileSize = fileSize;

            var events = Reader.ReadEventsFromLog(Server, fileDiff, 0);

            foreach (var ev in events)
            {
                Server.Manager.GetEventHandler().AddEvent(ev);
            }

            PreviousFileSize = fileSize;
        }
    }
}
