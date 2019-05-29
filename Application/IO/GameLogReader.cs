using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IW4MAdmin.Application.IO
{
    class GameLogReader : IGameLogReader
    {
        IEventParser Parser;
        readonly string LogFile;
        private bool? ignoreBots;

        public long Length => new FileInfo(LogFile).Length;

        public int UpdateInterval => 300;

        public GameLogReader(string logFile, IEventParser parser)
        {
            LogFile = logFile;
            Parser = parser;
        }

        public async Task<ICollection<GameEvent>> ReadEventsFromLog(Server server, long fileSizeDiff, long startPosition)
        {
            if (!ignoreBots.HasValue)
            {
                ignoreBots = server.Manager.GetApplicationSettings().Configuration().IgnoreBots;
            }

            // allocate the bytes for the new log lines
            List<string> logLines = new List<string>();

            // open the file as a stream
            using (var rd = new StreamReader(new FileStream(LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), Utilities.EncodingType))
            {
                // take the old start position and go back the number of new characters
                rd.BaseStream.Seek(-fileSizeDiff, SeekOrigin.End);

                string newLine;
                while (!string.IsNullOrEmpty(newLine = await rd.ReadLineAsync()))
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
                        var gameEvent = Parser.GenerateGameEvent(eventLine);
                        // we don't want to add the event if ignoreBots is on and the event comes from a bot
                        if (!ignoreBots.Value || (ignoreBots.Value && !((gameEvent.Origin?.IsBot ?? false) || (gameEvent.Target?.IsBot ?? false))))
                        {
                            gameEvent.Owner = server;

                            if ((gameEvent.RequiredEntity & GameEvent.EventRequiredEntity.Origin) == GameEvent.EventRequiredEntity.Origin && gameEvent.Origin.NetworkId != 1)
                            {
                                gameEvent.Origin = server.GetClientsAsList().First(_client => _client.NetworkId == gameEvent.Origin?.NetworkId);
                            }

                            if ((gameEvent.RequiredEntity & GameEvent.EventRequiredEntity.Target) == GameEvent.EventRequiredEntity.Target)
                            {
                                gameEvent.Target = server.GetClientsAsList().First(_client => _client.NetworkId == gameEvent.Target?.NetworkId);
                            }

                            if (gameEvent.Origin != null)
                            {
                                gameEvent.Origin.CurrentServer = server;
                            }

                            if (gameEvent.Target != null)
                            {
                                gameEvent.Target.CurrentServer = server;
                            }

                            events.Add(gameEvent);
                        }
                    }

                    catch (InvalidOperationException)
                    {
                        if (!ignoreBots.Value)
                        {
                            server.Logger.WriteWarning("Could not find client in client list when parsing event line");
                            server.Logger.WriteDebug(eventLine);
                        }
                    }

                    catch (Exception e)
                    {
                        server.Logger.WriteWarning("Could not properly parse event line");
                        server.Logger.WriteDebug(e.Message);
                        server.Logger.WriteDebug(eventLine);
                    }
                }
            }

            return events;
        }
    }
}
