using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharedLibraryCore.RCon
{
    class ConnectionState
    {
        public int ConnectionAttempts { get; set; }
        const int BufferSize = 4096;
        public readonly byte[] ReceiveBuffer = new byte[BufferSize];
        public readonly SemaphoreSlim OnComplete = new SemaphoreSlim(1, 1);
        public readonly ManualResetEventSlim OnSentData = new ManualResetEventSlim(false);
        public readonly ManualResetEventSlim OnReceivedData = new ManualResetEventSlim(false);
        public SocketAsyncEventArgs SendEventArgs { get; set; } = new SocketAsyncEventArgs();
        public SocketAsyncEventArgs ReceiveEventArgs { get; set; } = new SocketAsyncEventArgs();
        public DateTime LastQuery { get; set; } = DateTime.Now;
    }

    public class Connection
    {
        static readonly ConcurrentDictionary<EndPoint, ConnectionState> ActiveQueries = new ConcurrentDictionary<EndPoint, ConnectionState>();
        public IPEndPoint Endpoint { get; private set; }
        public string RConPassword { get; private set; }

        private readonly ILogger Log;
        private IRConParserConfiguration Config;
        private readonly Encoding defaultEncoding;

        public Connection(string ipAddress, int port, string password, ILogger log, IRConParserConfiguration config)
        {
            Endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            defaultEncoding = Encoding.GetEncoding("windows-1252");
            RConPassword = password;
            Log = log;
            Config = config;
        }

        public void SetConfiguration(IRConParserConfiguration config)
        {
            Config = config;
        }

        public async Task<string[]> SendQueryAsync(StaticHelpers.QueryType type, string parameters = "")
        {
            if (!ActiveQueries.ContainsKey(this.Endpoint))
            {
                ActiveQueries.TryAdd(this.Endpoint, new ConnectionState());
            }

            var connectionState = ActiveQueries[this.Endpoint];

#if DEBUG == true
            Log.WriteDebug($"Waiting for semaphore to be released [{this.Endpoint}]");
#endif
            // enter the semaphore so only one query is sent at a time per server.
            await connectionState.OnComplete.WaitAsync();

            var timeSinceLastQuery = (DateTime.Now - connectionState.LastQuery).TotalMilliseconds;

            if (timeSinceLastQuery < StaticHelpers.FloodProtectionInterval)
            {
                await Task.Delay(StaticHelpers.FloodProtectionInterval - (int)timeSinceLastQuery);
            }

            connectionState.LastQuery = DateTime.Now;

#if DEBUG == true
            Log.WriteDebug($"Semaphore has been released [{this.Endpoint}]");
            Log.WriteDebug($"Query [{this.Endpoint},{type.ToString()},{parameters}]");
#endif

            byte[] payload = null;
            bool waitForResponse = Config.WaitForResponse;

            string convertEncoding(string text)
            {   
                byte[] convertedBytes = Utilities.EncodingType.GetBytes(text);
                return defaultEncoding.GetString(convertedBytes);
            }

            try
            {
                string convertedRConPassword = convertEncoding(RConPassword);
                string convertedParameters = convertEncoding(parameters);

                switch (type)
                {
                    case StaticHelpers.QueryType.GET_DVAR:
                        waitForResponse |= true;
                        payload = string.Format(Config.CommandPrefixes.RConGetDvar, convertedRConPassword, convertedParameters + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.SET_DVAR:
                        payload = string.Format(Config.CommandPrefixes.RConSetDvar, convertedRConPassword, convertedParameters + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.COMMAND:
                        payload = string.Format(Config.CommandPrefixes.RConCommand, convertedRConPassword, convertedParameters + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.GET_STATUS:
                        waitForResponse |= true;
                        payload = (Config.CommandPrefixes.RConGetStatus + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.GET_INFO:
                        waitForResponse |= true;
                        payload = (Config.CommandPrefixes.RConGetInfo + '\0').Select(Convert.ToByte).ToArray();
                        break;
                    case StaticHelpers.QueryType.COMMAND_STATUS:
                        waitForResponse |= true;
                        payload = string.Format(Config.CommandPrefixes.RConCommand, convertedRConPassword, "status\0").Select(Convert.ToByte).ToArray();
                        break;
                }
            }

            // this happens when someone tries to send something that can't be converted into a 7 bit character set
            // e.g: emoji -> windows-1252
            catch (OverflowException)
            {
                connectionState.OnComplete.Release(1);
                throw new NetworkException($"Invalid character expected when converting encodings - {parameters}");
            }

            byte[] response = null;

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
#if DEBUG == true
                Log.WriteDebug($"Sending {payload.Length} bytes to [{this.Endpoint}] ({connectionState.ConnectionAttempts}/{StaticHelpers.AllowedConnectionFails})");
#endif
                try
                {
                    response = await SendPayloadAsync(payload, waitForResponse);

                    if (response.Length == 0 && waitForResponse)
                    {
                        throw new NetworkException("Expected response but got 0 bytes back");
                    }

                    connectionState.OnComplete.Release(1);
                    connectionState.ConnectionAttempts = 0;
                }

                catch
                {
                    if (connectionState.ConnectionAttempts < StaticHelpers.AllowedConnectionFails)
                    {
                        await Task.Delay(StaticHelpers.FloodProtectionInterval);
                        goto retrySend;
                    }

                    connectionState.OnComplete.Release(1);
                    throw new NetworkException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_COMMUNICATION"].FormatExt(Endpoint));
                }
            }

            string responseString = defaultEncoding.GetString(response, 0, response.Length) + '\n';

            if (responseString.Contains("Invalid password") || responseString.Contains("rconpassword"))
            {
                throw new NetworkException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_RCON_INVALID"]);
            }

            if (responseString.ToString().Contains("rcon_password"))
            {
                throw new NetworkException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_RCON_NOTSET"]);
            }

            string[] splitResponse = responseString.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .ToArray();
            return splitResponse;
        }

        private async Task<byte[]> SendPayloadAsync(byte[] payload, bool waitForResponse)
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
                if (!await Task.Run(() => connectionState.OnSentData.Wait(StaticHelpers.SocketTimeout)))
                {
                    rconSocket.Close();
                    throw new NetworkException("Timed out sending data", rconSocket);
                }
            }

            if (!waitForResponse)
            {
                return new byte[0];
            }

            connectionState.ReceiveEventArgs.SetBuffer(connectionState.ReceiveBuffer);

            // get our response back
            bool receiveDataPending = rconSocket.ReceiveFromAsync(connectionState.ReceiveEventArgs);

            if (receiveDataPending)
            {
                if (!await Task.Run(() => connectionState.OnReceivedData.Wait(StaticHelpers.SocketTimeout)))
                {
                    rconSocket.Close();
                    throw new NetworkException("Timed out waiting for response", rconSocket);
                }
            }

            rconSocket.Close();

            byte[] response = connectionState.ReceiveBuffer
                .Take(connectionState.ReceiveEventArgs.BytesTransferred)
                .ToArray();

            return response;
        }

        private void OnDataReceived(object sender, SocketAsyncEventArgs e)
        {
#if DEBUG == true
            Log.WriteDebug($"Read {e.BytesTransferred} bytes from {e.RemoteEndPoint.ToString()}");
#endif
            ActiveQueries[this.Endpoint].OnReceivedData.Set();
        }

        private void OnDataSent(object sender, SocketAsyncEventArgs e)
        {
#if DEBUG == true
            Log.WriteDebug($"Sent {e.Buffer.Length} bytes to {e.ConnectSocket.RemoteEndPoint.ToString()}");
#endif
            ActiveQueries[this.Endpoint].OnSentData.Set();
        }
    }
}
