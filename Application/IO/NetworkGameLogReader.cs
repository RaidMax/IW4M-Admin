using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Integrations.Cod;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.IO
{
    /// <summary>
    /// provides capability of reading log files over udp 
    /// </summary>
    class NetworkGameLogReader : IGameLogReader
    {
        private readonly IEventParser _eventParser;
        private readonly ILogger _logger;
        private readonly Uri _uri;
        private static readonly NetworkLogState State = new();
        private bool _stateRegistered;
        private CancellationToken _token;

        public NetworkGameLogReader(IReadOnlyList<Uri> uris, IEventParser parser, ILogger<NetworkGameLogReader> logger)
        {
            _eventParser = parser;
            _uri = uris[0];
            _logger = logger;
        }

        public long Length => -1;

        public int UpdateInterval => 150;

        public Task<IEnumerable<GameEvent>> ReadEventsFromLog(long fileSizeDiff, long startPosition,
            Server server = null)
        {
            // todo: other games might support this
            var serverEndpoint = (server?.RemoteConnection as CodRConConnection)?.Endpoint;

            if (serverEndpoint is null)
            {
                return Task.FromResult(Enumerable.Empty<GameEvent>());
            }

            if (!_stateRegistered && !State.EndPointExists(serverEndpoint))
            {
                try
                {
                    var client = State.RegisterEndpoint(serverEndpoint, BuildLocalEndpoint()).Client;

                    _stateRegistered = true;
                    _token = server.Manager.CancellationToken;

                    if (client == null)
                    {
                        using (LogContext.PushProperty("Server", server.ToString()))
                        {
                            _logger.LogInformation("Not registering {Name} socket because it is already bound",
                                nameof(NetworkGameLogReader));
                        }
                        return Task.FromResult(Enumerable.Empty<GameEvent>());
                    }

                    Task.Run(async () => await ReadNetworkData(client, _token), _token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not register {Name} endpoint {Endpoint}",
                        nameof(NetworkGameLogReader), _uri);
                    throw;
                }
            }

            var events = new List<GameEvent>();

            foreach (var logData in State.GetServerLogData(serverEndpoint)
                         .Select(log => Utilities.EncodingType.GetString(log)))
            {
                if (string.IsNullOrWhiteSpace(logData))
                {
                    return Task.FromResult(Enumerable.Empty<GameEvent>());
                }

                var lines = logData
                    .Split('\n')
                    .Where(line => line.Length > 0 && !line.Contains('ÿ'));

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
                        _logger.LogError(ex, "Could not properly parse event line from http {EventLine}",
                            eventLine);
                    }
                }
            }

            return Task.FromResult((IEnumerable<GameEvent>)events);
        }

        private async Task ReadNetworkData(UdpClient client, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // get more data
                IPEndPoint remoteEndpoint = null;
                byte[] bufferedData = null;

                if (client == null)
                {
                    // we already have a socket listening on this port for data, so we don't need to run another thread
                    break;
                }

                try
                {
                    var result = await client.ReceiveAsync(_token);
                    remoteEndpoint = result.RemoteEndPoint;
                    bufferedData = result.Buffer;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Stopping network log receive");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not receive lines for {LogReader}", nameof(NetworkGameLogReader));
                }

                if (bufferedData != null)
                {
                    State.QueueServerLogData(remoteEndpoint, bufferedData);
                }
            }
        }

        private IPEndPoint BuildLocalEndpoint()
        {
            try
            {
                return new IPEndPoint(Dns.GetHostAddresses(_uri.Host).First(), _uri.Port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not setup {LogReader} endpoint", nameof(NetworkGameLogReader));
                throw;
            }
        }
    }
}
