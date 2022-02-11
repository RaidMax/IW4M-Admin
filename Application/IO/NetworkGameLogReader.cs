using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.IO
{
    /// <summary>
    /// provides capability of reading log files over HTTP
    /// </summary>
    class NetworkGameLogReader : IGameLogReader
    {
        private readonly IEventParser _eventParser;
        private readonly UdpClient _udpClient;
        private readonly ILogger _logger;

        public NetworkGameLogReader(Uri[] uris, IEventParser parser, ILogger<NetworkGameLogReader> logger)
        {
            _eventParser = parser;
            try
            {
                var endPoint = new IPEndPoint(Dns.GetHostAddresses(uris[0].Host).First(), uris[0].Port);
                _udpClient = new UdpClient(endPoint);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could setup {LogReader}", nameof(NetworkGameLogReader));
            }

            _logger = logger;
        }

        public long Length => -1;

        public int UpdateInterval => 500;

        public async Task<IEnumerable<GameEvent>> ReadEventsFromLog(long fileSizeDiff, long startPosition)
        {
            if (_udpClient == null)
            {
                return Enumerable.Empty<GameEvent>();
            }

            byte[] buffer;
            try
            {
                buffer = (await _udpClient.ReceiveAsync()).Buffer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could receive lines for {LogReader}", nameof(NetworkGameLogReader));
                return Enumerable.Empty<GameEvent>();
            }

            if (!buffer.Any())
            {
                return Enumerable.Empty<GameEvent>();
            }

            var logData = Utilities.EncodingType.GetString(buffer);

            if (string.IsNullOrWhiteSpace(logData))
            {
                return Enumerable.Empty<GameEvent>();
            }

            var lines = logData
                .Split('\n')
                .Where(line => line.Length > 0);

            var events = new List<GameEvent>();
            foreach (var eventLine in lines)
            {
                try
                {
                    // this trim end should hopefully fix the nasty runaway regex
                    var gameEvent = _eventParser.GenerateGameEvent(eventLine.TrimEnd('\r'));
                    events.Add(gameEvent);
                }

                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not properly parse event line from http {eventLine}", eventLine);
                }
            }

            return events;
        }
    }
}
