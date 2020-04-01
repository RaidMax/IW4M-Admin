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
        private readonly IEventParser _eventParser;
        private readonly IGameLogServer _logServerApi;
        readonly string logPath;
        private string lastKey = "next";

        public GameLogReaderHttp(Uri gameLogServerUri, string logPath, IEventParser parser)
        {
            this.logPath = logPath.ToBase64UrlSafeString();
            _eventParser = parser;
            _logServerApi = RestClient.For<IGameLogServer>(gameLogServerUri);
        }

        public long Length => -1;

        public int UpdateInterval => 500;

        public async Task<IEnumerable<GameEvent>> ReadEventsFromLog(Server server, long fileSizeDiff, long startPosition)
        {
            var events = new List<GameEvent>();
            string b64Path = logPath;
            var response = await _logServerApi.Log(b64Path, lastKey);
            lastKey = response.NextKey;

            if (!response.Success && string.IsNullOrEmpty(lastKey))
            {
                server.Logger.WriteError($"Could not get log server info of {logPath}/{b64Path} ({server.LogPath})");
                return events;
            }

            else if (!string.IsNullOrWhiteSpace(response.Data))
            {
                // parse each line
                var lines = response.Data
                     .Split(Environment.NewLine)
                     .Where(_line => _line.Length > 0);

                foreach (string eventLine in lines)
                {
                    try
                    {
                        // this trim end should hopefully fix the nasty runaway regex
                        var gameEvent = _eventParser.GenerateGameEvent(eventLine.TrimEnd('\r'));
                        events.Add(gameEvent);
                    }

                    catch (Exception e)
                    {
                        server.Logger.WriteError("Could not properly parse event line from http");
                        server.Logger.WriteDebug(e.Message);
                        server.Logger.WriteDebug(eventLine);
                    }
                }
            }

            return events;
        }
    }
}
