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

        public GameLogReaderHttp(Uri gameLogServerUri, string logPath, IEventParser parser)
        {
            this.logPath = logPath.ToBase64UrlSafeString(); ;
            Parser = parser;
            Api = RestClient.For<IGameLogServer>(gameLogServerUri);
        }

        public long Length => -1;

        public int UpdateInterval => 350;

        public async Task<ICollection<GameEvent>> ReadEventsFromLog(Server server, long fileSizeDiff, long startPosition)
        {
#if DEBUG == true
            server.Logger.WriteDebug($"Begin reading from http log");
#endif

            if (!ignoreBots.HasValue)
            {
                ignoreBots = server.Manager.GetApplicationSettings().Configuration().IgnoreBots;
            }

            var events = new List<GameEvent>();
            string b64Path = logPath;
            var response = await Api.Log(b64Path);

            if (!response.Success)
            {
                server.Logger.WriteError($"Could not get log server info of {logPath}/{b64Path} ({server.LogPath})");
                return events;
            }

            // parse each line
            foreach (string eventLine in response.Data.Split(Environment.NewLine))
            {
                if (eventLine.Length > 0)
                {
                    try
                    {
                        var gameEvent = Parser.GenerateGameEvent(eventLine);
                        // we don't want to add the even if ignoreBots is on and the event comes froma bot
                        if (!ignoreBots.Value || (ignoreBots.Value && (gameEvent.Origin.NetworkId != -1 || gameEvent.Target.NetworkId != -1)))
                        {
                            gameEvent.Owner = server;
                            // we need to pull the "live" versions of the client (only if the client id isn't IW4MAdmin
                            gameEvent.Origin = gameEvent.Origin.ClientId == 1 ? gameEvent.Origin : server.GetClientsAsList().First(_client => _client.NetworkId == gameEvent.Origin.NetworkId);
                            gameEvent.Target = gameEvent.Target.ClientId == 1 ? gameEvent.Target : server.GetClientsAsList().First(_client => _client.NetworkId == gameEvent.Target.NetworkId);

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

            return events;
        }
    }
}
