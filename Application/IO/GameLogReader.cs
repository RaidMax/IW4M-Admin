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

        public ICollection<GameEvent> EventsFromLog(Server server, long fileSizeDiff, long startPosition)
        {
            // allocate the bytes for the new log lines
            List<string> logLines = new List<string>();

            // open the file as a stream
            using (var rd = new StreamReader(new FileStream(LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), Utilities.EncodingType))
            {
                // take the old start position and go back the number of new characters
                rd.BaseStream.Seek(-fileSizeDiff, SeekOrigin.End);
                // the difference should be in the range of a int :P
                string newLine;
                while (!String.IsNullOrEmpty(newLine = rd.ReadLine()))
                {
                    logLines.Add(newLine);
                }
            }

            List<GameEvent> events = new List<GameEvent>();

            // parse each line
            foreach (string eventLine in logLines)
            {
                if (eventLine.Length > 0)
                {
                    try
                    {
                        // todo: catch elsewhere
                        events.Add(Parser.GetEvent(server, eventLine));
                    }
                    
                    catch (Exception e)
                    {
                        Program.ServerManager.GetLogger().WriteWarning("Could not properly parse event line");
                        Program.ServerManager.GetLogger().WriteDebug(e.Message);
                        Program.ServerManager.GetLogger().WriteDebug(eventLine);
                    }
                }
            }

            return events;
        }
    }
}
