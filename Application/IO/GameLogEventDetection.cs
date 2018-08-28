using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.IO;
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

            Task.Run(async () =>
            {
               while (!server.Manager.ShutdownRequested())
               {
                   if ((server.Manager as ApplicationManager).IsInitialized)
                   {
                       OnEvent(new EventState()
                       {
                           Log = server.Manager.GetLogger(),
                           ServerId = server.ToString()
                       });
                   }
                   await Task.Delay(100);
               }
           });
        }

        private void OnEvent(object state)
        {
            long newLength = Reader.Length;

            try
            {
                UpdateLogEvents(newLength);
            }

            catch (Exception e)
            {
                ((EventState)state).Log.WriteWarning($"Failed to update log event for {((EventState)state).ServerId}");
                ((EventState)state).Log.WriteDebug($"Exception: {e.Message}");
                ((EventState)state).Log.WriteDebug($"StackTrace: {e.StackTrace}");
            }
        }

        private void UpdateLogEvents(long fileSize)
        {
            if (PreviousFileSize == 0)
                PreviousFileSize = fileSize;

            long fileDiff = fileSize - PreviousFileSize;

            if (fileDiff < 1)
                return;

            PreviousFileSize = fileSize;

            var events = Reader.EventsFromLog(Server, fileDiff, 0);
            foreach (var ev in events)
                Server.Manager.GetEventHandler().AddEvent(ev);

            PreviousFileSize = fileSize;
        }
    }
}
