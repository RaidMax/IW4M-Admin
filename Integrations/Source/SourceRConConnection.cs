using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Integrations.Source.Extensions;
using Integrations.Source.Interfaces;
using Microsoft.Extensions.Logging;
using RconSharp;
using Serilog.Context;
using SharedLibraryCore;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.RCon;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Integrations.Source
{
    public class SourceRConConnection : IRConConnection
    {
        private readonly ILogger _logger;
        private readonly string _password;
        private readonly IPEndPoint _ipEndPoint;
        private readonly IRConClientFactory _rconClientFactory;
        private readonly SemaphoreSlim _activeQuery;

        private static readonly TimeSpan FloodDelay = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(30);

        private DateTime _lastQuery = DateTime.Now;
        private RconClient _rconClient;
        private bool _authenticated;
        private bool _needNewSocket = true;

        public SourceRConConnection(ILogger<SourceRConConnection> logger, IRConClientFactory rconClientFactory,
            IPEndPoint ipEndPoint, string password)
        {
            _rconClientFactory = rconClientFactory;
            _password = password;
            _logger = logger;
            _ipEndPoint = ipEndPoint;
            _activeQuery = new SemaphoreSlim(1, 1);
        }

        ~SourceRConConnection()
        {
            _activeQuery.Dispose();
        }

        public async Task<string[]> SendQueryAsync(StaticHelpers.QueryType type, string parameters = "")
        {
            try
            {
                await _activeQuery.WaitAsync();
                await WaitForAvailable();

                if (_needNewSocket)
                {
                    try
                    {
                        _rconClient?.Disconnect();
                    }
                    catch
                    {
                        // ignored
                    }

                    await Task.Delay(ConnectionTimeout);
                    _rconClient = _rconClientFactory.CreateClient(_ipEndPoint);
                    _authenticated = false;
                    _needNewSocket = false;
                }

                using (LogContext.PushProperty("Server", _ipEndPoint.ToString()))
                {
                    _logger.LogDebug("Connecting to RCon socket");
                }

                await TryConnectAndAuthenticate().WithTimeout(ConnectionTimeout);

                var multiPacket = false;

                if (type == StaticHelpers.QueryType.COMMAND_STATUS)
                {
                    parameters = "status";
                    multiPacket = true;
                }

                parameters = parameters.ReplaceUnfriendlyCharacters();
                parameters = parameters.StripColors();

                using (LogContext.PushProperty("Server", _ipEndPoint.ToString()))
                {
                    _logger.LogDebug("Sending query {Type} with parameters \"{Parameters}\"", type, parameters);
                }

                var response = await _rconClient.ExecuteCommandAsync(parameters, multiPacket)
                    .WithTimeout(ConnectionTimeout);

                using (LogContext.PushProperty("Server", $"{_ipEndPoint}"))
                {
                    _logger.LogDebug("Received RCon response {Response}", response);
                }

                var split = response.TrimEnd('\n').Split('\n');
                return split.Take(split.Length - 1).ToArray();
            }

            catch (TaskCanceledException)
            {
                _needNewSocket = true;
                throw new NetworkException("Timeout while attempting to communicate with server");
            }

            catch (SocketException ex)
            {
                using (LogContext.PushProperty("Server", _ipEndPoint.ToString()))
                {
                    _logger.LogError(ex, "Socket exception encountered while attempting to communicate with server");
                }

                _needNewSocket = true;

                throw new NetworkException("Socket exception encountered while attempting to communicate with server");
            }

            catch (Exception ex) when (ex.GetType() != typeof(NetworkException) &&
                                       ex.GetType() != typeof(ServerException))
            {
                using (LogContext.PushProperty("Server", _ipEndPoint.ToString()))
                {
                    _logger.LogError(ex, "Could not execute RCon query {Parameters}", parameters);
                }

                throw new NetworkException("Unable to communicate with server");
            }

            finally
            {
                if (_activeQuery.CurrentCount == 0)
                {
                    _activeQuery.Release();
                }

                _lastQuery = DateTime.Now;
            }
        }

        private async Task WaitForAvailable()
        {
            var diff = DateTime.Now - _lastQuery;
            if (diff < FloodDelay)
            {
                await Task.Delay(FloodDelay - diff);
            }
        }

        private async Task TryConnectAndAuthenticate()
        {
            if (!_authenticated)
            {
                using (LogContext.PushProperty("Server", _ipEndPoint.ToString()))
                {
                    _logger.LogDebug("Authenticating to RCon socket");
                }

                await _rconClient.ConnectAsync().WithTimeout(ConnectionTimeout);
                _authenticated = await _rconClient.AuthenticateAsync(_password).WithTimeout(ConnectionTimeout);

                if (!_authenticated)
                {
                    using (LogContext.PushProperty("Server", _ipEndPoint.ToString()))
                    {
                        _logger.LogError("Could not login to server");
                    }

                    throw new ServerException("Could not authenticate to server with provided password");
                }
            }
        }

        public void SetConfiguration(IRConParser config)
        {
        }
    }
}
