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
                    using (var re = cl.GetAsync($"{LogFile}&length=1").Result)
                    {
                        using (var content = re.Content)
                        {
                            string response = content.ReadAsStringAsync().Result ?? "0";
                            return Convert.ToInt64(response);
                        }
                    }
                }
            }
        }

        public int UpdateInterval => 1000;

        public ICollection<GameEvent> ReadEventsFromLog(Server server, long fileSizeDiff, long startPosition)
        {
#if DEBUG == true
            server.Logger.WriteDebug($"Begin reading {fileSizeDiff} from http log");
#endif
            string log;
            using (var cl = new HttpClient())
            {
                using (var re = cl.GetAsync($"{LogFile}&start={fileSizeDiff}").Result)
                {
                    using (var content = re.Content)
                    {
                        log = content.ReadAsStringAsync().Result;
                    }
                }
            }
#if DEBUG == true
            server.Logger.WriteDebug($"retrieved events from http log");
#endif
            List<GameEvent> events = new List<GameEvent>();
            string[] lines = log.Split(Environment.NewLine);

#if DEBUG == true
            server.Logger.WriteDebug($"Begin parse of {lines.Length} lines from http log");
#endif

            // parse each line
            foreach (string eventLine in lines)
            {
                if (eventLine.Length > 0)
                {
                    try
                    {
                        // todo: catch elsewhere
                        var e = Parser.GetEvent(server, eventLine);
#if DEBUG == true
                        server.Logger.WriteDebug($"Parsed event with id {e.Id}  from http");
#endif
                        events.Add(e);
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
