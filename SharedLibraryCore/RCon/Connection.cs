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
    }

    public class Connection
    {
        static readonly ConcurrentDictionary<EndPoint, ConnectionState> ActiveQueries = new ConcurrentDictionary<EndPoint, ConnectionState>();
        public IPEndPoint Endpoint { get; private set; }
        public string RConPassword { get; private set; }
        ILogger Log;

        public Connection(string ipAddress, int port, string password, ILogger log)
        {
            Endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            RConPassword = password;
            Log = log;
        }

        public async Task<string[]> SendQueryAsync(StaticHelpers.QueryType type, string parameters = "", bool waitForResponse = true)
        {
            if (!ActiveQueries.ContainsKey(this.Endpoint))
            {
                ActiveQueries.TryAdd(this.Endpoint, new ConnectionState());
            }

            var connectionState = ActiveQueries[this.Endpoint];

#if DEBUG == true
            Log.WriteDebug($"Waiting for semaphore to be released [${this.Endpoint}]");
#endif
            await connectionState.OnComplete.WaitAsync();

#if DEBUG == true
            Log.WriteDebug($"Semaphore has been released [${this.Endpoint}]");
#endif

            byte[] payload = null;

            switch (type)
            {
                case StaticHelpers.QueryType.DVAR:
                case StaticHelpers.QueryType.COMMAND:
                    var header = "ÿÿÿÿrcon ".Select(Convert.ToByte).ToList();
                    byte[] p = Utilities.EncodingType.GetBytes($"{RConPassword} {parameters}");
                    header.AddRange(p);
                    payload = header.ToArray();
                    break;
                case StaticHelpers.QueryType.GET_STATUS:
                    payload = "ÿÿÿÿgetstatus".Select(Convert.ToByte).ToArray();
                    break;
                case StaticHelpers.QueryType.GET_INFO:
                    payload = "ÿÿÿÿgetinfo".Select(Convert.ToByte).ToArray();
                    break;
            }

            byte[] response = null;
            retrySend:
#if DEBUG == true
            Log.WriteDebug($"Sending {payload.Length} bytes to [${this.Endpoint}] ({connectionState.ConnectionAttempts++}/{StaticHelpers.AllowedConnectionFails})");
#endif
            try
            {
                response = await SendPayloadAsync(payload);
            }

            catch (Exception ex)
            {
                if(connectionState.ConnectionAttempts < StaticHelpers.AllowedConnectionFails)
                {
                    connectionState.ConnectionAttempts++;
                    Log.WriteWarning($"{Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_COMMUNICATION"]} [{this.Endpoint}] ({connectionState.ConnectionAttempts++}/{StaticHelpers.AllowedConnectionFails})");
                    await Task.Delay(StaticHelpers.SocketTimeout.Milliseconds);
                    goto retrySend;
                }
                ActiveQueries.TryRemove(this.Endpoint, out _);
                Log.WriteDebug(ex.GetExceptionInfo());
                throw new NetworkException($"{Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_COMMUNICATION"]} [{this.Endpoint}]");
            }

            ActiveQueries.TryRemove(this.Endpoint, out _);

            string responseString = Utilities.EncodingType.GetString(response, 0, response.Length).TrimEnd('\0') + '\n';

            if (responseString.Contains("Invalid password"))
            {
                // todo: localize this
                throw new NetworkException("RCON password is invalid");
            }

            if (responseString.ToString().Contains("rcon_password"))
            {
                throw new NetworkException("RCON password has not been set");
            }

            string[] splitResponse = responseString.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim()).ToArray();
            return splitResponse;
        }

        private async Task<byte[]> SendPayloadAsync(byte[] payload)
        {
            var rconSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            {
                DontFragment = true,
                Ttl = 42,
                ExclusiveAddressUse = true
            };

            var connectionState = ActiveQueries[this.Endpoint];

            var outgoingDataArgs = new SocketAsyncEventArgs()
            {
                RemoteEndPoint = this.Endpoint
            };
            outgoingDataArgs.SetBuffer(payload);
            outgoingDataArgs.Completed += OnDataSent;

            // send the data to the server
            bool sendDataPending = rconSocket.SendToAsync(outgoingDataArgs);

            var incomingDataArgs = new SocketAsyncEventArgs()
            {
                RemoteEndPoint = this.Endpoint
            };
            incomingDataArgs.SetBuffer(connectionState.ReceiveBuffer);
            incomingDataArgs.Completed += OnDataReceived;

            // get our response back
            rconSocket.ReceiveFromAsync(incomingDataArgs);

            if (!await connectionState.OnComplete.WaitAsync(StaticHelpers.SocketTimeout.Milliseconds))
            {
                throw new NetworkException("Timed out waiting for response", rconSocket);
            }

            byte[] response = connectionState.ReceiveBuffer;
            return response;
        }

        private void OnDataReceived(object sender, SocketAsyncEventArgs e)
        {
#if DEBUG == true
            Log.WriteDebug($"Read {e.BytesTransferred} bytes from {e.RemoteEndPoint.ToString()}");
#endif
            ActiveQueries[this.Endpoint].OnComplete.Release(1);
        }

        private void OnDataSent(object sender, SocketAsyncEventArgs e)
        {
#if DEBUG == true
            Log.WriteDebug($"Sent {e.Buffer.Length} bytes to {e.ConnectSocket.RemoteEndPoint.ToString()}");
#endif
        }
    }
}
