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
using System.Threading.Tasks;

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

        private IRConParserConfiguration config;
        private readonly ILogger _log;
        private readonly Encoding _gameEncoding;

        public RConConnection(string ipAddress, int port, string password, ILogger log, Encoding gameEncoding)
        {
            Endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            _gameEncoding = gameEncoding;
            RConPassword = password;
            _log = log;
        }

        public void SetConfiguration(IRConParserConfiguration config)
        {
            this.config = config;
        }

        public async Task<string[]> SendQueryAsync(StaticHelpers.QueryType type, string parameters = "")
        {
            if (!ActiveQueries.ContainsKey(this.Endpoint))
            {
                ActiveQueries.TryAdd(this.Endpoint, new ConnectionState());
            }

            var connectionState = ActiveQueries[this.Endpoint];

            if (Utilities.IsDevelopment)
            {
                _log.WriteDebug($"Waiting for semaphore to be released [{this.Endpoint}]");
            }

            // enter the semaphore so only one query is sent at a time per server.
            await connectionState.OnComplete.WaitAsync();

            var timeSinceLastQuery = (DateTime.Now - connectionState.LastQuery).TotalMilliseconds;

            if (timeSinceLastQuery < StaticHelpers.FloodProtectionInterval)
            {
                await Task.Delay(StaticHelpers.FloodProtectionInterval - (int)timeSinceLastQuery);
            }

            connectionState.LastQuery = DateTime.Now;

            if (Utilities.IsDevelopment)
            {
                _log.WriteDebug($"Semaphore has been released [{Endpoint}]");
                _log.WriteDebug($"Query [{Endpoint},{type},{parameters}]");
            }

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
            catch (OverflowException)
            {
                connectionState.OnComplete.Release(1);
                throw new NetworkException($"Invalid character encountered when converting encodings - {parameters}");
            }

            byte[][] response = null;

        retrySend:
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            {
                DontFragment = true,
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
#if DEBUG == true
                _log.WriteDebug($"Sending {payload.Length} bytes to [{this.Endpoint}] ({connectionState.ConnectionAttempts}/{StaticHelpers.AllowedConnectionFails})");
#endif
                try
                {
                    response = await SendPayloadAsync(payload, waitForResponse);

                    if ((response.Length == 0 || response[0].Length == 0) && waitForResponse)
                    {
                        throw new NetworkException("Expected response but got 0 bytes back");
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

                    throw new NetworkException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_COMMUNICATION"].FormatExt(Endpoint));
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
                _log.WriteWarning($"Received empty response for request [{type}, {parameters}, {Endpoint}]");
                return new string[0];
            }

            string responseString = type == StaticHelpers.QueryType.COMMAND_STATUS ?
               ReassembleSegmentedStatus(response) : RecombineMessages(response);

            // note: not all games respond if the pasword is wrong or not set
            if (responseString.Contains("Invalid password") || responseString.Contains("rconpassword"))
            {
                throw new NetworkException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_RCON_INVALID"]);
            }

            if (responseString.Contains("rcon_password"))
            {
                throw new NetworkException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_RCON_NOTSET"]);
            }

            if (responseString.Contains(config.ServerNotRunningResponse))
            {
                throw new ServerException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_NOT_RUNNING"].FormatExt(Endpoint.ToString()));
            }

            string responseHeaderMatch = Regex.Match(responseString, config.CommandPrefixes.RConResponse).Value;
            string[] headerSplit = responseString.Split(type == StaticHelpers.QueryType.GET_INFO ? config.CommandPrefixes.RconGetInfoResponseHeader : responseHeaderMatch);

            if (headerSplit.Length != 2)
            {
                throw new NetworkException("Unexpected response header from server");
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

        private async Task<byte[][]> SendPayloadAsync(byte[] payload, bool waitForResponse)
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
                // the send has not been completed asyncronously
                // this really shouldn't ever happen because it's UDP
                if (!await Task.Run(() => connectionState.OnSentData.Wait(StaticHelpers.SocketTimeout(1))))
                {
                    rconSocket.Close();
                    throw new NetworkException("Timed out sending data", rconSocket);
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
                if (Utilities.IsDevelopment)
                {
                    _log.WriteDebug($"Waiting to asynchrously receive data on attempt #{connectionState.ConnectionAttempts}");
                }
                if (!await Task.Run(() => connectionState.OnReceivedData.Wait(StaticHelpers.SocketTimeout(connectionState.ConnectionAttempts))))
                {
                    rconSocket.Close();
                    throw new NetworkException("Timed out waiting for response", rconSocket);
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
            if (Utilities.IsDevelopment)
            {
                _log.WriteDebug($"Read {e.BytesTransferred} bytes from {e.RemoteEndPoint}");
            }

            // this occurs when we close the socket
            if (e.BytesTransferred == 0)
            {
                ActiveQueries[this.Endpoint].OnReceivedData.Set();
                return;
            }

            if (sender is Socket sock)
            {
                var state = ActiveQueries[this.Endpoint];
                state.BytesReadPerSegment.Add(e.BytesTransferred);

                try
                {
                    // we still have available data so the payload was segmented
                    if (sock.Available > 0)
                    {
                        state.ReceiveEventArgs.SetBuffer(state.ReceiveBuffer, e.BytesTransferred, state.ReceiveBuffer.Length - e.BytesTransferred);

                        if (!sock.ReceiveAsync(state.ReceiveEventArgs))
                        {
                            if (Utilities.IsDevelopment)
                            {
                                _log.WriteDebug($"Read {state.ReceiveEventArgs.BytesTransferred} synchronous bytes from {e.RemoteEndPoint}");
                            }
                            // we need to increment this here because the callback isn't executed if there's no pending IO
                            state.BytesReadPerSegment.Add(state.ReceiveEventArgs.BytesTransferred);
                            ActiveQueries[this.Endpoint].OnReceivedData.Set();
                        }
                    }

                    else
                    {
                        ActiveQueries[this.Endpoint].OnReceivedData.Set();
                    }
                }

                catch (ObjectDisposedException)
                {
                    ActiveQueries[this.Endpoint].OnReceivedData.Set();
                }
            }
        }

        private void OnDataSent(object sender, SocketAsyncEventArgs e)
        {
            if (Utilities.IsDevelopment)
            {
                _log.WriteDebug($"Sent {e.Buffer?.Length} bytes to {e.ConnectSocket?.RemoteEndPoint?.ToString()}");
            }
            ActiveQueries[this.Endpoint].OnSentData.Set();
        }
    }
}
