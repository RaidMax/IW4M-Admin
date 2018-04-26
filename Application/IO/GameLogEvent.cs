using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IW4MAdmin.Application.IO
{
    class GameLogEvent
    {
        FileSystemWatcher LogPathWatcher;
        Server Server;
        long PreviousFileSize;
        GameLogReader Reader;
        Timer RefreshInfoTimer;
        string GameLogFile;

        public GameLogEvent(Server server, string gameLogPath, string gameLogName)
        {
            GameLogFile = gameLogPath;
            Reader = new GameLogReader(gameLogPath, server.EventParser);
            Server = server;
            RefreshInfoTimer = new Timer((sender) => 
            {
                long newLength = new FileInfo(GameLogFile).Length;
                UpdateLogEvents(newLength);

            }, null, 0, 100);
            /*LogPathWatcher = new FileSystemWatcher()
            {
                Path = gameLogPath.Replace(gameLogName, ""),
                Filter = gameLogName,
                NotifyFilter = (NotifyFilters)383,
                InternalBufferSize = 4096
            };

           // LogPathWatcher.Changed += LogPathWatcher_Changed;
            LogPathWatcher.EnableRaisingEvents = true;*/
        }

        /*
        ~GameLogEvent()
        {
            LogPathWatcher.EnableRaisingEvents = false;
        }*/

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
