using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;

namespace IW4MAdmin.Application.IO
{
    /// <summary>
    /// provides capibility of reading log files over HTTP
    /// </summary>
    class GameLogReaderHttp : IGameLogReader
    {
        readonly IEventParser Parser;
        readonly string LogFile;

        public GameLogReaderHttp(string logFile, IEventParser parser)
        {
            LogFile = logFile;
            Parser = parser;
        }

        public long Length
        {
            get
            {
                using (var cl = new HttpClient())
                {
                    using (var re = cl.GetAsync($"{LogFile}?length=1").Result)
                    {
                        using (var content = re.Content)
                        {
                            return Convert.ToInt64(content.ReadAsStringAsync().Result ?? "0");
                        }
                    }
                }
            }
        }

        public int UpdateInterval => 1000;

        public ICollection<GameEvent> EventsFromLog(Server server, long fileSizeDiff, long startPosition)
        {
            string log;
            using (var cl = new HttpClient())
            {
                using (var re = cl.GetAsync($"{LogFile}?start={fileSizeDiff}").Result)
                {
                    using (var content = re.Content)
                    {
                        log = content.ReadAsStringAsync().Result;
                    }
                }
            }

            List<GameEvent> events = new List<GameEvent>();

            // parse each line
            foreach (string eventLine in log.Split(Environment.NewLine))
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
