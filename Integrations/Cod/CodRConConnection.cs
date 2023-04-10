using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SharedLibraryCore;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.RCon;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Integrations.Cod
{
    /// <summary>
    /// implementation of IRConConnection
    /// </summary>
    public class CodRConConnection : IRConConnection
    {
        private static readonly ConcurrentDictionary<EndPoint, ConnectionState> ActiveQueries = new();
        public IPEndPoint Endpoint { get; }
        public string RConPassword { get; }

        private IRConParser _parser;
        private IRConParserConfiguration _config;
        private readonly ILogger _log;
        private readonly Encoding _gameEncoding;
        private readonly int _retryAttempts;

        public CodRConConnection(IPEndPoint ipEndpoint, string password, ILogger<CodRConConnection> log,
            Encoding gameEncoding, int retryAttempts)
        {
            RConPassword = password;
            _gameEncoding = gameEncoding;
            _log = log;
            Endpoint = ipEndpoint;
            _retryAttempts = retryAttempts;
        }

        public void SetConfiguration(IRConParser parser)
        {
            _parser = parser;
            _config = parser.Configuration;
        }

        public async Task<string[]> SendQueryAsync(StaticHelpers.QueryType type, string parameters = "",
            CancellationToken token = default)
        {
            try
            {
                return await SendQueryAsyncInternal(type, parameters, token);
            }
            catch (RConException ex) when (ex.IsOperationCancelled)
            {
                _log.LogDebug(ex, "Could not complete RCon request");
                throw;
            }
            catch (Exception ex)
            {
                using (LogContext.PushProperty("Server", Endpoint.ToString()))
                {
                    _log.LogWarning(ex, "Could not complete RCon request");
                }

                throw;
            }
            finally
            {
                _log.LogDebug("Releasing OnComplete {Count}", ActiveQueries[Endpoint].OnComplete.CurrentCount);

                if (ActiveQueries[Endpoint].OnComplete.CurrentCount == 0)
                {
                    ActiveQueries[Endpoint].OnComplete.Release();
                }
            }
        }

        private async Task<string[]> SendQueryAsyncInternal(StaticHelpers.QueryType type, string parameters = "",
            CancellationToken token = default)
        {
            if (!ActiveQueries.ContainsKey(Endpoint))
            {
                ActiveQueries.TryAdd(Endpoint, new ConnectionState());
            }

            if (!ActiveQueries.TryGetValue(Endpoint, out var connectionState))
            {
                using (LogContext.PushProperty("Server", Endpoint.ToString()))
                {
                    _log.LogError("Could not retrieve connection state");
                }

                throw new InvalidOperationException("Could not get connection state");
            }

            _log.LogDebug("Waiting for semaphore to be released [{Endpoint}]", Endpoint);

            // enter the semaphore so only one query is sent at a time per server.
            try
            {
                await connectionState.OnComplete.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                _log.LogDebug("OnComplete did not complete before timeout {Count}",
                    connectionState.OnComplete.CurrentCount);
                throw new RConException("Timed out waiting for access to rcon socket", true);
            }

            var timeSinceLastQuery = (DateTime.Now - connectionState.LastQuery).TotalMilliseconds;

            if (timeSinceLastQuery < _config.FloodProtectInterval)
            {
                try
                {
                    var delay = _config.FloodProtectInterval - (int)timeSinceLastQuery;
                    _log.LogDebug("Delaying for {Delay}ms", delay);
                    await Task.Delay(delay, token);
                }
                catch (OperationCanceledException)
                {
                    _log.LogDebug("Waiting for flood protect did not complete before timeout timeout {Count}",
                        connectionState.OnComplete.CurrentCount);
                    throw new RConException("Timed out waiting for flood protect to expire", true);
                }
            }

            _log.LogDebug("Semaphore has been released [{Endpoint}]", Endpoint);
            _log.LogDebug("Query {@QueryInfo}", new { endpoint = Endpoint.ToString(), type, parameters });

            byte[] payload = null;
            var waitForResponse = _config.WaitForResponse;

            string ConvertEncoding(string text)
            {
                var convertedBytes = Utilities.EncodingType.GetBytes(text);
                return _gameEncoding.GetString(convertedBytes);
            }

            try
            {
                var convertedRConPassword = ConvertEncoding(RConPassword);
                var convertedParameters = ConvertEncoding(parameters);

                switch (type)
                {
                    case StaticHelpers.QueryType.GET_DVAR:
                        waitForResponse = true;
                        payload = string
                            .Format(_config.CommandPrefixes.RConGetDvar, convertedRConPassword,
                                convertedParameters + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.SET_DVAR:
                        payload = string
                            .Format(_config.CommandPrefixes.RConSetDvar, convertedRConPassword,
                                convertedParameters + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.COMMAND:
                        payload = string
                            .Format(_config.CommandPrefixes.RConCommand, convertedRConPassword,
                                convertedParameters + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.GET_STATUS:
                        waitForResponse = true;
                        payload = (_config.CommandPrefixes.RConGetStatus + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.GET_INFO:
                        waitForResponse = true;
                        payload = (_config.CommandPrefixes.RConGetInfo + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.COMMAND_STATUS:
                        waitForResponse = true;
                        payload = string.Format(_config.CommandPrefixes.RConCommand, convertedRConPassword, "status\0")
                            .Select(Convert.ToByte).ToArray();
                        break;
                }
            }

            // this happens when someone tries to send something that can't be converted into a 7 bit character set
            // e.g: emoji -> windows-1252
            catch (OverflowException ex)
            {
                using (LogContext.PushProperty("Server", Endpoint.ToString()))
                {
                    _log.LogError(ex, "Could not convert RCon data payload to desired encoding {Encoding} {Params}",
                        _gameEncoding.EncodingName, parameters);
                }

                throw new RConException("Invalid character encountered when converting encodings");
            }

            byte[][] response;

            retrySend:
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                   {
                       DontFragment = false,
                       Ttl = 100,
                       ExclusiveAddressUse = true,
                   })
            {
                if (!token.IsCancellationRequested)
                {
                    connectionState.ConnectionAttempts++;
                }

                connectionState.ReceivedBytes.Clear();

                _log.LogDebug(
                    "Sending {PayloadLength} bytes to [{Endpoint}] ({ConnectionAttempts}/{AllowedConnectionFailures}) parameters {Payload}",
                    payload.Length, Endpoint, connectionState.ConnectionAttempts, _retryAttempts, parameters);

                try
                {
                    connectionState.LastQuery = DateTime.Now;
                    var retryTimeout = StaticHelpers.SocketTimeout(connectionState.ConnectionAttempts);
                    var overrideTimeout = _parser.OverrideTimeoutForCommand(parameters);
                    var maxTimeout = !overrideTimeout.HasValue || overrideTimeout == TimeSpan.Zero
                        ? retryTimeout
                        : overrideTimeout.Value;

                    using var internalTokenSource = new CancellationTokenSource(maxTimeout);
                    using var chainedTokenSource =
                        CancellationTokenSource.CreateLinkedTokenSource(token, internalTokenSource.Token);

                    if (connectionState.ConnectionAttempts > 1)
                    {
                        using (LogContext.PushProperty("Server", Endpoint.ToString()))
                        {
                            _log.LogInformation(
                                "Retrying RCon message ({ConnectionAttempts}/{AllowedConnectionFailures} attempts, {Timeout}ms timeout) with parameters {Payload}",
                                connectionState.ConnectionAttempts, _retryAttempts,
                                maxTimeout.TotalMilliseconds, parameters);
                        }
                    }

                    waitForResponse = waitForResponse && overrideTimeout.HasValue;
                    response = await SendPayloadAsync(socket, payload, waitForResponse, chainedTokenSource.Token);

                    if ((response?.Length == 0 || response[0].Length == 0) && waitForResponse)
                    {
                        _log.LogDebug("0 bytes received from rcon request");
                        throw new RConException("Expected response but got 0 bytes back");
                    }

                    connectionState.ConnectionAttempts = 0;
                }

                catch (OperationCanceledException)
                {
                    _log.LogDebug("OperationCanceledException when waiting for payload send to complete");

                    // if we timed out due to the cancellation token,
                    // we don't want to count that as an attempt
                    if (token.IsCancellationRequested)
                    {
                        if (connectionState.ConnectionAttempts > 0)
                        {
                            connectionState.ConnectionAttempts--;
                        }

                        throw new RConException("Timed out waiting on retry delay for RCon socket",
                            token.IsCancellationRequested);
                    }

                    if (connectionState.ConnectionAttempts < _retryAttempts)
                    {
                        goto retrySend;
                    }

                    using (LogContext.PushProperty("Server", Endpoint.ToString()))
                    {
                        _log.LogWarning(
                            "Made {ConnectionAttempts} attempts to send RCon data to server, but received no response",
                            connectionState.ConnectionAttempts);
                    }

                    connectionState.ConnectionAttempts = 0;
                    throw new NetworkException("Reached maximum retry attempts to send RCon data to server");
                }
                catch (Exception ex)
                {
                    _log.LogDebug(ex, "RCon Exception");

                    if (connectionState.ConnectionAttempts < _retryAttempts)
                    {
                        goto retrySend;
                    }

                    using (LogContext.PushProperty("Server", Endpoint.ToString()))
                    {
                        _log.LogWarning(
                            "Made {ConnectionAttempts} attempts to send RCon data to server, but received no response",
                            connectionState.ConnectionAttempts);
                    }

                    connectionState.ConnectionAttempts = 0;
                    throw new NetworkException("Reached maximum retry attempts to send RCon data to server");
                }
            }

            // at this point we can run in parallel and the next request can start because we have our data
            if (response.Length == 0)
            {
                _log.LogDebug("Received empty response for RCon request {@Query}",
                    new { endpoint = Endpoint.ToString(), type, parameters });
                return Array.Empty<string>();
            }

            var responseString = type == StaticHelpers.QueryType.COMMAND_STATUS
                ? ReassembleSegmentedStatus(response)
                : RecombineMessages(response);

            var validatedResponse = ValidateResponse(type, responseString);

            return validatedResponse;
        }

        private async Task<byte[][]> SendPayloadAsync(Socket rconSocket, byte[] payload, bool waitForResponse,
            CancellationToken token = default)
        {
            var connectionState = ActiveQueries[Endpoint];

            if (rconSocket is null)
            {
                _log.LogDebug("Invalid state");
                throw new InvalidOperationException("State is not valid for socket operation");
            }

            var sentByteCount = await rconSocket.SendToAsync(payload, SocketFlags.None, Endpoint, token);
            var complete = sentByteCount == payload.Length;

            if (!complete)
            {
                using (LogContext.PushProperty("Server", Endpoint.ToString()))
                {
                    _log.LogWarning("Could not send data to remote RCon socket on attempt #{ConnectionAttempts}",
                        connectionState.ConnectionAttempts);
                }

                rconSocket.Close();
                throw new NetworkException("Could not send data to remote RCon socket", rconSocket);
            }

            if (!waitForResponse)
            {
                return Array.Empty<byte[]>();
            }

            _log.LogDebug("Waiting to asynchronously receive data on attempt #{ConnectionAttempts}",
                connectionState.ConnectionAttempts);

            await ReceiveAndStoreSocketData(rconSocket, token, connectionState);

            if (_parser.GameName is Server.Game.IW3 or Server.Game.T4)
            {
                await Task.Delay(100, token); // CoD4x shenanigans 
            }

            while (rconSocket.Available > 0)
            {
                await ReceiveAndStoreSocketData(rconSocket, token, connectionState);
            }

            rconSocket.Close();
            return GetResponseData(connectionState);
        }

        private async Task ReceiveAndStoreSocketData(Socket rconSocket, CancellationToken token,
            ConnectionState connectionState)
        {
            var result = await rconSocket.ReceiveFromAsync(connectionState.ReceiveBuffer,
                SocketFlags.None, Endpoint, token);

            if (result.ReceivedBytes == 0)
            {
                return;
            }

            var storageBuffer = new byte[result.ReceivedBytes];
            Array.Copy(connectionState.ReceiveBuffer, storageBuffer, storageBuffer.Length);
            connectionState.ReceivedBytes.Add(storageBuffer);
        }

        #region Helpers

        private string[] ValidateResponse(StaticHelpers.QueryType type, string responseString)
        {
            // note: not all games respond if the password is wrong or not set
            if (responseString.Contains("Invalid password", StringComparison.InvariantCultureIgnoreCase) ||
                responseString.Contains("rconpassword"))
            {
                throw new RConException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_RCON_INVALID"]);
            }

            if (responseString.Contains("rcon_password"))
            {
                throw new RConException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_RCON_NOTSET"]);
            }

            if (responseString.Contains(_config.ServerNotRunningResponse))
            {
                throw new ServerException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_NOT_RUNNING"]
                    .FormatExt(Endpoint.ToString()));
            }

            var responseHeaderMatch = Regex.Match(responseString, _config.CommandPrefixes.RConResponse).Value;
            var headerSplit = responseString.Split(type == StaticHelpers.QueryType.GET_INFO
                ? _config.CommandPrefixes.RconGetInfoResponseHeader
                : responseHeaderMatch);

            if (headerSplit.Length == 2)
            {
                return headerSplit.Last().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.StartsWith("^7") ? line[2..] : line).ToArray();
            }

            using (LogContext.PushProperty("Server", Endpoint.ToString()))
            {
                _log.LogWarning("Invalid response header from server. Expected {Expected}, but got {Response}",
                    _config.CommandPrefixes.RConResponse, headerSplit.FirstOrDefault());
            }

            throw new RConException("Unexpected response header from server");
        }

        /// <summary>
        /// reassembles broken status segments into the 'correct' ordering
        /// <remarks>this is primarily for T7, and is really only reliable for 2 segments</remarks>
        /// </summary>
        /// <param name="segments">array of segmented byte arrays</param>
        /// <returns></returns>
        private string ReassembleSegmentedStatus(IEnumerable<byte[]> segments)
        {
            var splitStatusStrings = new List<string>();

            foreach (var segment in segments)
            {
                var responseString = _gameEncoding.GetString(segment, 0, segment.Length);
                var statusHeaderMatch = _config.StatusHeader.PatternMatcher.Match(responseString);
                if (statusHeaderMatch.Success)
                {
                    splitStatusStrings.Insert(0, responseString.TrimEnd('\0'));
                }

                else
                {
                    splitStatusStrings.Add(responseString.Replace(_config.CommandPrefixes.RConResponse, "")
                        .TrimEnd('\0'));
                }
            }

            return string.Join("", splitStatusStrings);
        }
        
        /// <summary>
        /// Recombines multiple game messages into one
        /// </summary>
        /// <param name="payload"></param>
        /// <returns></returns>
        private string RecombineMessages(IReadOnlyList<byte[]> payload)
        {
            if (payload.Count == 1)
            {
                return _gameEncoding.GetString(payload[0]).TrimEnd('\n') + '\n';
            }

            var builder = new StringBuilder();
            for (var i = 0; i < payload.Count; i++)
            {
                var message = _gameEncoding.GetString(payload[i]).TrimEnd('\n') + '\n';
                if (i > 0)
                {
                    message = message.Replace(_config.CommandPrefixes.RConResponse, "");
                }

                builder.Append(message);
            }

            builder.Append('\n');
            return builder.ToString();
        }

        private static byte[][] GetResponseData(ConnectionState connectionState)
        {
            return connectionState.ReceivedBytes.ToArray();
        }
        
        #endregion
    }
}
