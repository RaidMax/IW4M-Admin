using SharedLibraryCore;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.RCon;
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
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.RCon
{
    /// <summary>
    /// implementation of IRConConnection
    /// </summary>
    public class RConConnection : IRConConnection
    {
        static readonly ConcurrentDictionary<EndPoint, ConnectionState> ActiveQueries = new ConcurrentDictionary<EndPoint, ConnectionState>();
        public IPEndPoint Endpoint { get; private set; }
        public string RConPassword { get; private set; }

        private IRConParser parser;
        private IRConParserConfiguration config;
        private readonly ILogger _log;
        private readonly Encoding _gameEncoding;

        public RConConnection(string ipAddress, int port, string password, ILogger<RConConnection> log, Encoding gameEncoding)
        {
            Endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            _gameEncoding = gameEncoding;
            RConPassword = password;
            _log = log;
        }

        public void SetConfiguration(IRConParser parser)
        {
            this.parser = parser;
            config = parser.Configuration;
        }

        public async Task<string[]> SendQueryAsync(StaticHelpers.QueryType type, string parameters = "")
        {
            if (!ActiveQueries.ContainsKey(this.Endpoint))
            {
                ActiveQueries.TryAdd(this.Endpoint, new ConnectionState());
            }

            var connectionState = ActiveQueries[this.Endpoint];

            _log.LogDebug("Waiting for semaphore to be released [{endpoint}]", Endpoint);

            // enter the semaphore so only one query is sent at a time per server.
            await connectionState.OnComplete.WaitAsync();

            var timeSinceLastQuery = (DateTime.Now - connectionState.LastQuery).TotalMilliseconds;

            if (timeSinceLastQuery < StaticHelpers.FloodProtectionInterval)
            {
                await Task.Delay(StaticHelpers.FloodProtectionInterval - (int)timeSinceLastQuery);
            }

            connectionState.LastQuery = DateTime.Now;

            _log.LogDebug("Semaphore has been released [{endpoint}]", Endpoint);
            _log.LogDebug("Query {@queryInfo}", new { endpoint=Endpoint.ToString(), type, parameters });

            byte[] payload = null;
            bool waitForResponse = config.WaitForResponse;

            string convertEncoding(string text)
            {
                byte[] convertedBytes = Utilities.EncodingType.GetBytes(text);
                return _gameEncoding.GetString(convertedBytes);
            }

            try
            {
                string convertedRConPassword = convertEncoding(RConPassword);
                string convertedParameters = convertEncoding(parameters);

                switch (type)
                {
                    case StaticHelpers.QueryType.GET_DVAR:
                        waitForResponse |= true;
                        payload = string.Format(config.CommandPrefixes.RConGetDvar, convertedRConPassword, convertedParameters + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.SET_DVAR:
                        payload = string.Format(config.CommandPrefixes.RConSetDvar, convertedRConPassword, convertedParameters + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.COMMAND:
                        payload = string.Format(config.CommandPrefixes.RConCommand, convertedRConPassword, convertedParameters + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.GET_STATUS:
                        waitForResponse |= true;
                        payload = (config.CommandPrefixes.RConGetStatus + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.GET_INFO:
                        waitForResponse |= true;
                        payload = (config.CommandPrefixes.RConGetInfo + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.COMMAND_STATUS:
                        waitForResponse |= true;
                        payload = string.Format(config.CommandPrefixes.RConCommand, convertedRConPassword, "status\0").Select(Convert.ToByte).ToArray();
                        break;
                }
            }

            // this happens when someone tries to send something that can't be converted into a 7 bit character set
            // e.g: emoji -> windows-1252
            catch (OverflowException ex)
            {
                connectionState.OnComplete.Release(1);
                using (LogContext.PushProperty("Server", Endpoint.ToString()))
                {
                    _log.LogError(ex, "Could not convert RCon data payload to desired encoding {encoding} {params}",
                        _gameEncoding.EncodingName, parameters);
                }

                throw new RConException($"Invalid character encountered when converting encodings");
            }

            byte[][] response = null;

            retrySend:
            if (connectionState.ConnectionAttempts > 1)
            {
                using (LogContext.PushProperty("Server", Endpoint.ToString()))
                {
                    _log.LogInformation(
                        "Retrying RCon message ({connectionAttempts}/{allowedConnectionFailures} attempts) with parameters {payload}", 
                        connectionState.ConnectionAttempts, 
                        StaticHelpers.AllowedConnectionFails, parameters);
                }
            }
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            {
                DontFragment = false,
                Ttl = 100,
                ExclusiveAddressUse = true,
            })
            {
                connectionState.SendEventArgs.UserToken = socket;
                connectionState.OnSentData.Reset();
                connectionState.OnReceivedData.Reset();
                connectionState.ConnectionAttempts++;
                connectionState.BytesReadPerSegment.Clear();
                bool exceptionCaught = false;

                _log.LogDebug("Sending {payloadLength} bytes to [{endpoint}] ({connectionAttempts}/{allowedConnectionFailures})",
                payload.Length, Endpoint, connectionState.ConnectionAttempts, StaticHelpers.AllowedConnectionFails);

                try
                {
                    
                    response = await SendPayloadAsync(payload, waitForResponse, parser.OverrideTimeoutForCommand(parameters));

                    if ((response.Length == 0 || response[0].Length == 0) && waitForResponse)
                    {
                        throw new RConException("Expected response but got 0 bytes back");
                    }

                    connectionState.ConnectionAttempts = 0;
                }

                catch
                {
                    // we want to retry with a delay
                    if (connectionState.ConnectionAttempts < StaticHelpers.AllowedConnectionFails)
                    {
                        exceptionCaught = true;
                        await Task.Delay(StaticHelpers.SocketTimeout(connectionState.ConnectionAttempts));
                        goto retrySend;
                    }

                    using (LogContext.PushProperty("Server", Endpoint.ToString()))
                    {
                        _log.LogWarning(
                            "Made {connectionAttempts} attempts to send RCon data to server, but received no response",
                            connectionState.ConnectionAttempts);
                    }
                    connectionState.ConnectionAttempts = 0;
                    throw new NetworkException("Reached maximum retry attempts to send RCon data to server");
                }

                finally
                {
                    // we don't want to release if we're going to retry the query
                    if (connectionState.OnComplete.CurrentCount == 0 && !exceptionCaught)
                    {
                        connectionState.OnComplete.Release(1);
                    }
                }
            }

            if (response.Length == 0)
            {
                _log.LogDebug("Received empty response for RCon request {@query}", new { endpoint=Endpoint.ToString(), type, parameters });
                return new string[0];
            }

            string responseString = type == StaticHelpers.QueryType.COMMAND_STATUS ?
               ReassembleSegmentedStatus(response) : RecombineMessages(response);

            // note: not all games respond if the pasword is wrong or not set
            if (responseString.Contains("Invalid password") || responseString.Contains("rconpassword"))
            {
                throw new RConException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_RCON_INVALID"]);
            }

            if (responseString.Contains("rcon_password"))
            {
                throw new RConException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_RCON_NOTSET"]);
            }

            if (responseString.Contains(config.ServerNotRunningResponse))
            {
                throw new ServerException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_NOT_RUNNING"].FormatExt(Endpoint.ToString()));
            }

            string responseHeaderMatch = Regex.Match(responseString, config.CommandPrefixes.RConResponse).Value;
            string[] headerSplit = responseString.Split(type == StaticHelpers.QueryType.GET_INFO ? config.CommandPrefixes.RconGetInfoResponseHeader : responseHeaderMatch);

            if (headerSplit.Length != 2)
            {
                using (LogContext.PushProperty("Server", Endpoint.ToString()))
                {
                    _log.LogWarning("Invalid response header from server. Expected {expected}, but got {response}",
                        config.CommandPrefixes.RConResponse, headerSplit.FirstOrDefault());
                }

                throw new RConException("Unexpected response header from server");
            }

            string[] splitResponse = headerSplit.Last().Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return splitResponse;
        }

        /// <summary>
        /// reassembles broken status segments into the 'correct' ordering
        /// <remarks>this is primarily for T7, and is really only reliable for 2 segments</remarks>
        /// </summary>
        /// <param name="segments">array of segmented byte arrays</param>
        /// <returns></returns>
        public string ReassembleSegmentedStatus(byte[][] segments)
        {
            var splitStatusStrings = new List<string>();

            foreach (byte[] segment in segments)
            {
                string responseString = _gameEncoding.GetString(segment, 0, segment.Length);
                var statusHeaderMatch = config.StatusHeader.PatternMatcher.Match(responseString);
                if (statusHeaderMatch.Success)
                {
                    splitStatusStrings.Insert(0, responseString.TrimEnd('\0'));
                }

                else
                {
                    splitStatusStrings.Add(responseString.Replace(config.CommandPrefixes.RConResponse, "").TrimEnd('\0'));
                }
            }

            return string.Join("", splitStatusStrings);
        }

        /// <summary>
        /// Recombines multiple game messages into one
        /// </summary>
        /// <param name="payload"></param>
        /// <returns></returns>
        private string RecombineMessages(byte[][] payload)
        {
            if (payload.Length == 1)
            {
                return _gameEncoding.GetString(payload[0]).TrimEnd('\n') + '\n';
            }

            else
            {
                var builder = new StringBuilder();
                for (int i = 0; i < payload.Length; i++)
                {
                    string message = _gameEncoding.GetString(payload[i]).TrimEnd('\n') + '\n';
                    if (i > 0)
                    {
                        message = message.Replace(config.CommandPrefixes.RConResponse, "");
                    }
                    builder.Append(message);
                }
                builder.Append('\n');
                return builder.ToString();
            }
        }

        private async Task<byte[][]> SendPayloadAsync(byte[] payload, bool waitForResponse, TimeSpan overrideTimeout)
        {
            var connectionState = ActiveQueries[this.Endpoint];
            var rconSocket = (Socket)connectionState.SendEventArgs.UserToken;

            if (connectionState.ReceiveEventArgs.RemoteEndPoint == null &&
                connectionState.SendEventArgs.RemoteEndPoint == null)
            {
                // setup the event handlers only once because we're reusing the event args
                connectionState.SendEventArgs.Completed += OnDataSent;
                connectionState.ReceiveEventArgs.Completed += OnDataReceived;
                connectionState.SendEventArgs.RemoteEndPoint = this.Endpoint;
                connectionState.ReceiveEventArgs.RemoteEndPoint = this.Endpoint;
                connectionState.ReceiveEventArgs.DisconnectReuseSocket = true;
                connectionState.SendEventArgs.DisconnectReuseSocket = true;
            }

            connectionState.SendEventArgs.SetBuffer(payload);

            // send the data to the server
            bool sendDataPending = rconSocket.SendToAsync(connectionState.SendEventArgs);

            if (sendDataPending)
            {
                // the send has not been completed asynchronously
                // this really shouldn't ever happen because it's UDP
                if (!await Task.Run(() => connectionState.OnSentData.Wait(StaticHelpers.SocketTimeout(1))))
                {
                    using(LogContext.PushProperty("Server", Endpoint.ToString()))
                    {
                        _log.LogWarning("Socket timed out while sending RCon data on attempt {attempt}",
                            connectionState.ConnectionAttempts);
                    }
                    rconSocket.Close();
                    throw new NetworkException("Timed out sending RCon data", rconSocket);
                }
            }

            if (!waitForResponse)
            {
                return new byte[0][];
            }

            connectionState.ReceiveEventArgs.SetBuffer(connectionState.ReceiveBuffer);

            // get our response back
            bool receiveDataPending = rconSocket.ReceiveFromAsync(connectionState.ReceiveEventArgs);

            if (receiveDataPending)
            {
                _log.LogDebug("Waiting to asynchronously receive data on attempt #{connectionAttempts}", connectionState.ConnectionAttempts);
                if (!await Task.Run(() => connectionState.OnReceivedData.Wait(
                    new[]
                    {
                        StaticHelpers.SocketTimeout(connectionState.ConnectionAttempts), 
                        overrideTimeout
                    }.Max())))
                {
                    if (connectionState.ConnectionAttempts > 1) // this reduces some spam for unstable connections
                    {
                        using (LogContext.PushProperty("Server", Endpoint.ToString()))
                        {
                            _log.LogWarning(
                                "Socket timed out while waiting for RCon response on attempt {attempt} with timeout delay of {timeout}",
                                connectionState.ConnectionAttempts,
                                StaticHelpers.SocketTimeout(connectionState.ConnectionAttempts));
                        }
                    }

                    rconSocket.Close();
                    throw new NetworkException("Timed out receiving RCon response", rconSocket);
                }
            }

            rconSocket.Close();

            return GetResponseData(connectionState);
        }

        private byte[][] GetResponseData(ConnectionState connectionState)
        {
            var responseList = new List<byte[]>();
            int totalBytesRead = 0;

            foreach (int bytesRead in connectionState.BytesReadPerSegment)
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
            _log.LogDebug("Read {bytesTransferred} bytes from {endpoint}", e.BytesTransferred, e.RemoteEndPoint);

            // this occurs when we close the socket
            if (e.BytesTransferred == 0)
            {
                _log.LogDebug("No bytes were transmitted so the connection was probably closed");
                ActiveQueries[this.Endpoint].OnReceivedData.Set();
                return;
            }

            if (!(sender is Socket sock))
            {
                return;
            }
            
            var state = ActiveQueries[this.Endpoint];
            state.BytesReadPerSegment.Add(e.BytesTransferred);

            // I don't even want to know why this works for getting more data from Cod4x
            // but I'm leaving it in here as long as it doesn't break anything.
            // it's very stupid...
            Thread.Sleep(150);

            try
            {
                var totalBytesTransferred = e.BytesTransferred;
                _log.LogDebug("{total} total bytes transferred with {available} bytes remaining", totalBytesTransferred, sock.Available);
                // we still have available data so the payload was segmented
                while (sock.Available > 0)
                {
                    _log.LogDebug("{available} more bytes to be read", sock.Available);

                    var bufferSpaceAvailable = sock.Available + totalBytesTransferred - state.ReceiveBuffer.Length;
                    if (bufferSpaceAvailable >= 0 )
                    {
                        _log.LogWarning("Not enough buffer space to store incoming data {bytesNeeded} additional bytes required", bufferSpaceAvailable);
                        continue;
                    }

                    state.ReceiveEventArgs.SetBuffer(state.ReceiveBuffer, totalBytesTransferred, sock.Available);

                    if (sock.ReceiveAsync(state.ReceiveEventArgs))
                    {
                        _log.LogDebug("Remaining bytes are async");
                        continue;
                    }
                        
                    _log.LogDebug("Read {bytesTransferred} synchronous bytes from {endpoint}", state.ReceiveEventArgs.BytesTransferred, e.RemoteEndPoint);
                    // we need to increment this here because the callback isn't executed if there's no pending IO
                    state.BytesReadPerSegment.Add(state.ReceiveEventArgs.BytesTransferred);
                    totalBytesTransferred += state.ReceiveEventArgs.BytesTransferred;
                }
                    
                ActiveQueries[this.Endpoint].OnReceivedData.Set();
            }

            catch (ObjectDisposedException)
            {
                _log.LogDebug("Socket was disposed while receiving data");
                ActiveQueries[this.Endpoint].OnReceivedData.Set();
            }
        }

        private void OnDataSent(object sender, SocketAsyncEventArgs e)
        {
            _log.LogDebug("Sent {byteCount} bytes to {endpoint}", e.Buffer?.Length, e.ConnectSocket?.RemoteEndPoint);
            ActiveQueries[this.Endpoint].OnSentData.Set();
        }
    }
}
