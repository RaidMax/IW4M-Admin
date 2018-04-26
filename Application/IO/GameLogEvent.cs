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
        FileInfo Info;

        public GameLogEvent(Server server, string gameLogPath, string gameLogName)
        {
            GameLogFile = gameLogPath;
            Reader = new GameLogReader(gameLogPath, server.EventParser);
            Server = server;
            RefreshInfoTimer = new Timer((sender) => 
            {
                var newInfo = new FileInfo(GameLogFile);
                if (newInfo.Length - Info?.Length > 0)
                    LogPathWatcher_Changed(this, new FileSystemEventArgs(WatcherChangeTypes.Changed, "", ""));
                Info = newInfo;

            }, null, 0, 100);
            LogPathWatcher = new FileSystemWatcher()
            {
                Path = gameLogPath.Replace(gameLogName, ""),
                Filter = gameLogName,
                NotifyFilter = (NotifyFilters)383,
                InternalBufferSize = 4096
            };

            LogPathWatcher.Changed += LogPathWatcher_Changed;
            LogPathWatcher.EnableRaisingEvents = true;
        }

        ~GameLogEvent()
        {
            LogPathWatcher.EnableRaisingEvents = false;
        }

        private void LogPathWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            // retrieve the new file size 
            long newFileSize = new FileInfo(GameLogFile).Length;

            if (PreviousFileSize == 0)
                PreviousFileSize = newFileSize;

            long fileDiff = newFileSize - PreviousFileSize;

            if (fileDiff < 1)
                return;

            var events = Reader.EventsFromLog(Server, fileDiff);
            foreach (var ev in events)
                Server.Manager.GetEventHandler().AddEvent(ev);

            PreviousFileSize = newFileSize;
        }
    }
}
