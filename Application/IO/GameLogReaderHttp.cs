using IW4MAdmin.Application.API.GameLogServer;
using RestEase;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IW4MAdmin.Application.IO
{
    /// <summary>
    /// provides capability of reading log files over HTTP
    /// </summary>
    class GameLogReaderHttp : IGameLogReader
    {
        private readonly IEventParser _eventParser;
        private readonly IGameLogServer _logServerApi;
        private readonly ILogger _logger;
        private readonly string _safeLogPath;
        private string lastKey = "next";

        public GameLogReaderHttp(Uri[] gameLogServerUris, IEventParser parser, ILogger logger)
        {
            _eventParser = parser;
            _logServerApi = RestClient.For<IGameLogServer>(gameLogServerUris[0].ToString());
            _safeLogPath = gameLogServerUris[1].LocalPath.ToBase64UrlSafeString();
            _logger = logger;
        }

        public long Length => -1;

        public int UpdateInterval => 500;

        public async Task<IEnumerable<GameEvent>> ReadEventsFromLog(long fileSizeDiff, long startPosition)
        {
            var events = new List<GameEvent>();
            var response = await _logServerApi.Log(_safeLogPath, lastKey);
            lastKey = response.NextKey;

            if (!response.Success && string.IsNullOrEmpty(lastKey))
            {
                _logger.WriteError($"Could not get log server info of {_safeLogPath}");
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
                        _logger.WriteError("Could not properly parse event line from http");
                        _logger.WriteDebug(e.Message);
                        _logger.WriteDebug(eventLine);
                    }
                }
            }

            return events;
        }
    }
}
