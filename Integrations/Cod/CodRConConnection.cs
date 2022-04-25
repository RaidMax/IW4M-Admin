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
                using (LogContext.PushProperty("Server", Endpoint.ToString()))
                {
                    _log.LogDebug("Releasing OnComplete {Count}", ActiveQueries[Endpoint].OnComplete.CurrentCount);
                }

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
                _log.LogError("Could not retrieve connection state");
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
                throw new RConException("Timed out waiting for access to rcon socket");
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
                    throw new RConException("Timed out waiting for flood protect to expire");
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
                        waitForResponse |= true;
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
                        waitForResponse |= true;
                        payload = (_config.CommandPrefixes.RConGetStatus + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.GET_INFO:
                        waitForResponse |= true;
                        payload = (_config.CommandPrefixes.RConGetInfo + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.COMMAND_STATUS:
                        waitForResponse |= true;
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

            byte[][] response = null;

            retrySend:
            if (connectionState.ConnectionAttempts > 1)
            {
                using (LogContext.PushProperty("Server", Endpoint.ToString()))
                {
                    _log.LogInformation(
                        "Retrying RCon message ({ConnectionAttempts}/{AllowedConnectionFailures} attempts) with parameters {Payload}",
                        connectionState.ConnectionAttempts,
                        _retryAttempts, parameters);
                }
            }

            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                   {
                       DontFragment = false,
                       Ttl = 100,
                       ExclusiveAddressUse = true,
                   })
            {
                // wait for send to be ready
                try
                {
                    await connectionState.OnSentData.WaitAsync(token);
                }
                catch (OperationCanceledException)
                {
                    _log.LogDebug("OnSent did not complete in time");
                    throw new RConException("Timed out waiting for access to RCon send socket");
                }

                // wait for receive to be ready
                try
                {
                    await connectionState.OnReceivedData.WaitAsync(token);
                }
                catch (OperationCanceledException)
                {
                    _log.LogDebug("OnReceived did not complete in time");
                    if (connectionState.OnSentData.CurrentCount == 0)
                    {
                        connectionState.OnSentData.Release();
                    }

                    throw new RConException("Timed out waiting for access to RCon receive socket");
                }

                connectionState.SendEventArgs.UserToken = new ConnectionUserToken
                {
                    Socket = socket,
                    CancellationToken = token
                };

                connectionState.ConnectionAttempts++;
                connectionState.BytesReadPerSegment.Clear();

                _log.LogDebug(
                    "Sending {PayloadLength} bytes to [{Endpoint}] ({ConnectionAttempts}/{AllowedConnectionFailures})",
                    payload.Length, Endpoint, connectionState.ConnectionAttempts, _retryAttempts);

                try
                {
                    connectionState.LastQuery = DateTime.Now;
                    response = await SendPayloadAsync(payload, waitForResponse,
                        _parser.OverrideTimeoutForCommand(parameters), token);

                    if ((response?.Length == 0 || response[0].Length == 0) && waitForResponse)
                    {
                        _log.LogDebug("0 bytes received from rcon request");
                        throw new RConException("Expected response but got 0 bytes back");
                    }

                    connectionState.ConnectionAttempts = 0;
                }

                catch (OperationCanceledException)
                {
                    // if we timed out due to the cancellation token,
                    // we don't want to count that as an attempt
                    _log.LogDebug("OperationCanceledException when waiting for payload send to complete");
                    connectionState.ConnectionAttempts = 0;
                }
                catch
                {
                    // we want to retry with a delay
                    if (connectionState.ConnectionAttempts < _retryAttempts)
                    {
                        try
                        {
                            await Task.Delay(StaticHelpers.SocketTimeout(connectionState.ConnectionAttempts), token);
                        }
                        catch (OperationCanceledException)
                        {
                            _log.LogDebug("OperationCancelled while waiting for retry");
                            throw;
                        }

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
                finally
                {
                    try
                    {
                        if (connectionState.OnSentData.CurrentCount == 0)
                        {
                            connectionState.OnSentData.Release();
                        }

                        if (connectionState.OnReceivedData.CurrentCount == 0)
                        {
                            connectionState.OnReceivedData.Release();
                        }
                    }
                    catch
                    {
                        // ignored because we can have the socket operation cancelled (which releases the semaphore) but 
                        // this thread is not notified because it's an event
                    }
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

            if (headerSplit.Length != 2)
            {
                using (LogContext.PushProperty("Server", Endpoint.ToString()))
                {
                    _log.LogWarning("Invalid response header from server. Expected {Expected}, but got {Response}",
                        _config.CommandPrefixes.RConResponse, headerSplit.FirstOrDefault());
                }

                throw new RConException("Unexpected response header from server");
            }

            var splitResponse = headerSplit.Last().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return splitResponse;
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

        private async Task<byte[][]> SendPayloadAsync(byte[] payload, bool waitForResponse, TimeSpan overrideTimeout,
            CancellationToken token = default)
        {
            var connectionState = ActiveQueries[Endpoint];
            var rconSocket = ((ConnectionUserToken)connectionState.SendEventArgs.UserToken)?.Socket;

            if (rconSocket is null)
            {
                _log.LogDebug("Invalid state");
                throw new InvalidOperationException("State is not valid for socket operation");
            }

            if (connectionState.ReceiveEventArgs.RemoteEndPoint == null &&
                connectionState.SendEventArgs.RemoteEndPoint == null)
            {
                // setup the event handlers only once because we're reusing the event args
                connectionState.SendEventArgs.Completed += OnDataSent;
                connectionState.ReceiveEventArgs.Completed += OnDataReceived;
                connectionState.ReceiveEventArgs.UserToken = connectionState.SendEventArgs.UserToken;
                connectionState.SendEventArgs.RemoteEndPoint = Endpoint;
                connectionState.ReceiveEventArgs.RemoteEndPoint = Endpoint;
                connectionState.ReceiveEventArgs.DisconnectReuseSocket = true;
                connectionState.SendEventArgs.DisconnectReuseSocket = true;
            }

            connectionState.SendEventArgs.SetBuffer(payload);

            // send the data to the server
            var sendDataPending = rconSocket.SendToAsync(connectionState.SendEventArgs);

            if (sendDataPending)
            {
                // the send has not been completed asynchronously
                // this really shouldn't ever happen because it's UDP
                var complete = await connectionState.OnSentData.WaitAsync(StaticHelpers.SocketTimeout(4), token);

                if (!complete)
                {
                    using (LogContext.PushProperty("Server", Endpoint.ToString()))
                    {
                        _log.LogWarning("Socket timed out while sending RCon data on attempt {Attempt}",
                            connectionState.ConnectionAttempts);
                    }

                    rconSocket.Close();
                    throw new NetworkException("Timed out sending RCon data", rconSocket);
                }
            }

            if (!waitForResponse)
            {
                return Array.Empty<byte[]>();
            }

            connectionState.ReceiveEventArgs.SetBuffer(connectionState.ReceiveBuffer);

            // get our response back
            var receiveDataPending = rconSocket.ReceiveFromAsync(connectionState.ReceiveEventArgs);

            if (receiveDataPending)
            {
                _log.LogDebug("Waiting to asynchronously receive data on attempt #{ConnectionAttempts}",
                    connectionState.ConnectionAttempts);

                var completed = false;

                try
                {
                    completed = await connectionState.OnReceivedData.WaitAsync(
                        new[]
                        {
                            StaticHelpers.SocketTimeout(connectionState.ConnectionAttempts),
                            overrideTimeout
                        }.Max(), token);
                }
                catch (OperationCanceledException)
                {
                    // ignored
                }

                if (!completed)
                {
                    if (connectionState.ConnectionAttempts > 1) // this reduces some spam for unstable connections
                    {
                        using (LogContext.PushProperty("Server", Endpoint.ToString()))
                        {
                            _log.LogWarning(
                                "Socket timed out while waiting for RCon response on attempt {Attempt} with timeout delay of {Timeout}",
                                connectionState.ConnectionAttempts,
                                StaticHelpers.SocketTimeout(connectionState.ConnectionAttempts));
                        }
                    }

                    rconSocket.Close();
                    _log.LogDebug("OnDataReceived did not complete in allocated time");
                    throw new NetworkException("Timed out receiving RCon response", rconSocket);
                }
            }

            rconSocket.Close();
            return GetResponseData(connectionState);
        }

        private static byte[][] GetResponseData(ConnectionState connectionState)
        {
            var responseList = new List<byte[]>();
            var totalBytesRead = 0;

            foreach (var bytesRead in connectionState.BytesReadPerSegment)
            {
                responseList.Add(connectionState.ReceiveBuffer
                    .Skip(totalBytesRead)
                    .Take(bytesRead)
                    .ToArray());

                totalBytesRead += bytesRead;
            }

            return responseList.ToArray();
        }

        private void OnDataReceived(object sender, SocketAsyncEventArgs e)
        {
            _log.LogDebug("Read {BytesTransferred} bytes from {Endpoint}", e.BytesTransferred,
                e.RemoteEndPoint?.ToString());

            // this occurs when we close the socket
            if (e.BytesTransferred == 0)
            {
                _log.LogDebug("No bytes were transmitted so the connection was probably closed");

                var semaphore = ActiveQueries[Endpoint].OnReceivedData;

                try
                {
                    if (semaphore.CurrentCount == 0)
                    {
                        semaphore.Release();
                    }
                }
                catch
                {
                    // ignored because we can have the socket operation cancelled (which releases the semaphore) but 
                    // this thread is not notified because it's an event
                }

                return;
            }

            var state = ActiveQueries[Endpoint];
            var cancellationRequested = ((ConnectionUserToken)e.UserToken)?.CancellationToken.IsCancellationRequested ??
                                        false;

            if (sender is not Socket sock || cancellationRequested)
            {
                var semaphore = ActiveQueries[Endpoint].OnReceivedData;

                try
                {
                    if (semaphore.CurrentCount == 0)
                    {
                        semaphore.Release();
                    }
                }
                catch
                {
                    // ignored because we can have the socket operation cancelled (which releases the semaphore) but 
                    // this thread is not notified because it's an event
                }

                return;
            }

            state.BytesReadPerSegment.Add(e.BytesTransferred);

            // I don't even want to know why this works for getting more data from Cod4x
            // but I'm leaving it in here as long as it doesn't break anything.
            // it's very stupid...
            Thread.Sleep(150);

            try
            {
                var totalBytesTransferred = e.BytesTransferred;
                _log.LogDebug("{Total} total bytes transferred with {Available} bytes remaining", totalBytesTransferred,
                    sock.Available);

                // we still have available data so the payload was segmented
                while (sock.Available > 0)
                {
                    _log.LogDebug("{Available} more bytes to be read", sock.Available);

                    var bufferSpaceAvailable = sock.Available + totalBytesTransferred - state.ReceiveBuffer.Length;
                    if (bufferSpaceAvailable >= 0)
                    {
                        _log.LogWarning(
                            "Not enough buffer space to store incoming data {BytesNeeded} additional bytes required",
                            bufferSpaceAvailable);
                        continue;
                    }

                    state.ReceiveEventArgs.SetBuffer(state.ReceiveBuffer, totalBytesTransferred, sock.Available);
                    if (sock.ReceiveAsync(state.ReceiveEventArgs))
                    {
                        _log.LogDebug("Remaining bytes are async");
                        continue;
                    }

                    _log.LogDebug("Read {BytesTransferred} synchronous bytes from {Endpoint}",
                        state.ReceiveEventArgs.BytesTransferred, e.RemoteEndPoint?.ToString());

                    // we need to increment this here because the callback isn't executed if there's no pending IO
                    state.BytesReadPerSegment.Add(state.ReceiveEventArgs.BytesTransferred);
                    totalBytesTransferred += state.ReceiveEventArgs.BytesTransferred;
                }
            }

            catch (ObjectDisposedException)
            {
                _log.LogDebug("Socket was disposed while receiving data");
            }

            finally
            {
                var semaphore = ActiveQueries[Endpoint].OnReceivedData;
                try
                {
                    if (semaphore.CurrentCount == 0)
                    {
                        semaphore.Release();
                    }
                }
                catch
                {
                    // ignored because we can have the socket operation cancelled (which releases the semaphore) but 
                    // this thread is not notified because it's an event
                }
            }
        }

        private void OnDataSent(object sender, SocketAsyncEventArgs e)
        {
            _log.LogDebug("Sent {ByteCount} bytes to {Endpoint}", e.Buffer?.Length,
                e.ConnectSocket?.RemoteEndPoint?.ToString());

            var semaphore = ActiveQueries[Endpoint].OnSentData;
            try
            {
                if (semaphore.CurrentCount == 0)
                {
                    semaphore.Release();
                }
            }
            catch
            {
                // ignored because we can have the socket operation cancelled (which releases the semaphore) but 
                // this thread is not notified because it's an event
            }
        }
    }
}
