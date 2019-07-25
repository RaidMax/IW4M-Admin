using IW4MAdmin.Application.API.GameLogServer;
using RestEase;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using static SharedLibraryCore.Utilities;

namespace IW4MAdmin.Application.IO
{
    /// <summary>
    /// provides capibility of reading log files over HTTP
    /// </summary>
    class GameLogReaderHttp : IGameLogReader
    {
        readonly IEventParser Parser;
        readonly IGameLogServer Api;
        readonly string logPath;
        private bool? ignoreBots;
        private string lastKey = "next";

        public GameLogReaderHttp(Uri gameLogServerUri, string logPath, IEventParser parser)
        {
            this.logPath = logPath.ToBase64UrlSafeString(); ;
            Parser = parser;
            Api = RestClient.For<IGameLogServer>(gameLogServerUri);
        }

        public long Length => -1;

        public int UpdateInterval => 250;

        public async Task<ICollection<GameEvent>> ReadEventsFromLog(Server server, long fileSizeDiff, long startPosition)
        {
#if DEBUG == true
            server.Logger.WriteDebug($"Begin reading from http log at {DateTime.Now.Millisecond}");
#endif

            if (!ignoreBots.HasValue)
            {
                ignoreBots = server.Manager.GetApplicationSettings().Configuration().IgnoreBots;
            }

            var events = new List<GameEvent>();
            string b64Path = logPath;
            var response = await Api.Log(b64Path, lastKey);
            lastKey = response.NextKey;

            if (!response.Success && string.IsNullOrEmpty(lastKey))
            {
                server.Logger.WriteError($"Could not get log server info of {logPath}/{b64Path} ({server.LogPath})");
                return events;
            }

            else if (response.Data != null)
            {
                // parse each line
                foreach (string eventLine in response.Data.Split(Environment.NewLine))
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
#if DEBUG == true
                            server.Logger.WriteDebug($"Parsed event with id {gameEvent.Id}  from http");
#endif
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
                            server.Logger.WriteWarning("Could not properly parse remote event line");
                            server.Logger.WriteDebug(e.Message);
                            server.Logger.WriteDebug(eventLine);
                        }
                    }
                }
            }

#if DEBUG == true
            server.Logger.WriteDebug($"End reading from http log at {DateTime.Now.Millisecond}");
#endif
            return events;
        }
    }
}
