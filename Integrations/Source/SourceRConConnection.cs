using System;
using System.Linq;
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
        private readonly string _hostname;
        private readonly int _port;
        private readonly IRConClientFactory _rconClientFactory;
        private readonly SemaphoreSlim _activeQuery;

        private static readonly TimeSpan FloodDelay = TimeSpan.FromMilliseconds(250);
        
        private DateTime _lastQuery = DateTime.Now;
        private RconClient _rconClient;

        public SourceRConConnection(ILogger<SourceRConConnection> logger, IRConClientFactory rconClientFactory,
            string hostname, int port, string password)
        {
            _rconClientFactory = rconClientFactory;
            _password = password;
            _hostname = hostname;
            _port = port;
            _logger = logger;
            _rconClient = _rconClientFactory.CreateClient(_hostname, _port);
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
                var diff = DateTime.Now - _lastQuery;
                if (diff < FloodDelay)
                {
                    await Task.Delay(FloodDelay - diff);
                }

                using (LogContext.PushProperty("Server", $"{_hostname}:{_port}"))
                {
                    _logger.LogDebug("Connecting to RCon socket");
                }

                await _rconClient.ConnectAsync();

                bool authenticated;

                try
                {
                    using (LogContext.PushProperty("Server", $"{_hostname}:{_port}"))
                    {
                        _logger.LogDebug("Authenticating to RCon socket");
                    }

                    authenticated = await _rconClient.AuthenticateAsync(_password);
                }
                catch (SocketException ex)
                {
                    // occurs when the server comes back from hibernation
                    // this is probably a bug in the library
                    if (ex.ErrorCode == 10053 || ex.ErrorCode == 10054)
                    {
                        using (LogContext.PushProperty("Server", $"{_hostname}:{_port}"))
                        {
                            _logger.LogWarning(ex,
                                "Server appears to resumed from hibernation, so we are using a new socket");
                        }

                        try
                        {
                            _rconClient.Disconnect();
                        }
                        catch
                        {
                            // ignored
                        }

                        _rconClient = _rconClientFactory.CreateClient(_hostname, _port);
                    }

                    using (LogContext.PushProperty("Server", $"{_hostname}:{_port}"))
                    {
                        _logger.LogError(ex, "Error occurred authenticating with server");
                    }

                    throw new NetworkException("Error occurred authenticating with server");
                }

                if (!authenticated)
                {
                    using (LogContext.PushProperty("Server", $"{_hostname}:{_port}"))
                    {
                        _logger.LogError("Could not login to server");
                    }

                    throw new ServerException("Could not authenticate to server with provided password");
                }

                if (type == StaticHelpers.QueryType.COMMAND_STATUS)
                {
                    parameters = "status";
                }

                parameters = parameters.ReplaceUnfriendlyCharacters();
                parameters = parameters.StripColors();

                using (LogContext.PushProperty("Server", $"{_hostname}:{_port}"))
                {
                    _logger.LogDebug("Sending query {Type} with parameters \"{Parameters}\"", type, parameters);
                }

                var response = await _rconClient.ExecuteCommandAsync(parameters, true);

                using (LogContext.PushProperty("Server", $"{_rconClient.Host}:{_rconClient.Port}"))
                {
                    _logger.LogDebug("Received RCon response {Response}", response);
                }

                var split = response.TrimEnd('\n').Split('\n');
                return split.Take(split.Length - 1).ToArray();
            }

            catch (Exception ex) when (ex.GetType() != typeof(NetworkException) &&
                                       ex.GetType() != typeof(ServerException))
            {
                using (LogContext.PushProperty("Server", $"{_hostname}:{_port}"))
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

        public void SetConfiguration(IRConParser config)
        {
        }
    }
}