using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IW4MAdmin.Application.IO
{
    class GameLogReader
    {
        IEventParser Parser;
        string LogFile;

        public GameLogReader(string logFile, IEventParser parser)
        {
            LogFile = logFile;
            Parser = parser;
        }

        public ICollection<GameEvent> EventsFromLog(Server server, long fileSizeDiff)
        {
            // allocate the bytes for the new log lines
            byte[] fileBytes = new byte[fileSizeDiff];

            // open the file as a stream
            using (var rd = new BinaryReader(new FileStream(LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), Utilities.EncodingType))
            {
                rd.BaseStream.Seek(rd.BaseStream.Length - fileSizeDiff - 1, SeekOrigin.Begin);
                // the difference should be in the range of a int :P
                rd.Read(fileBytes, 0, (int)fileSizeDiff);
            }

            // convert to event line list
            string[] logLines = Utilities.EncodingType.GetString(fileBytes).Replace("\r", "").Split('\n');

            List<GameEvent> events = new List<GameEvent>();

            // parse each line
            foreach (string eventLine in logLines)
            {
                if (eventLine.Length > 0)
                    events.Add(Parser.GetEvent(server, eventLine));
            }

            return events;
        }
    }
}
