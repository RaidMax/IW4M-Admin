using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Interfaces;
using System;
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
        public Socket Client { get; private set; }
        public int BufferSize { get; private set; }
        public byte[] Buffer { get; private set; }

        private readonly StringBuilder sb;

        public StringBuilder ResponseString
        {
            get => sb;
        }

        public ConnectionState(Socket cl)
        {
            BufferSize = 8192;
            Buffer = new byte[BufferSize];
            Client = cl;
            sb = new StringBuilder();
        }
    }

    public class Connection
    {
        IPEndPoint Endpoint;
        string RConPassword;
        Socket ServerConnection;
        ILogger Log;
        int FailedSends;
        int FailedReceives;
        DateTime LastQuery;
        string response;

        ManualResetEvent OnConnected;
        ManualResetEvent OnSent;
        ManualResetEvent OnReceived;

        public Connection(string ipAddress, int port, string password, ILogger log)
        {
            Endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            RConPassword = password;
            Log = log;

            OnConnected = new ManualResetEvent(false);
            OnSent = new ManualResetEvent(false);
            OnReceived = new ManualResetEvent(false);

            try
            {
                ServerConnection = new Socket(Endpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                ServerConnection.BeginConnect(Endpoint, new AsyncCallback(OnConnectedCallback), ServerConnection);
                if (!OnConnected.WaitOne(StaticHelpers.SocketTimeout))
                    throw new SocketException((int)SocketError.TimedOut);
                FailedSends = 0;
            }

            catch (SocketException e)
            {
                throw new NetworkException(e.Message);
            }
        }

        ~Connection()
        {
            ServerConnection.Shutdown(SocketShutdown.Both);
            ServerConnection.Close();
            ServerConnection.Dispose();
        }

        private void OnConnectedCallback(IAsyncResult ar)
        {
            var serverSocket = (Socket)ar.AsyncState;

            try
            {
                serverSocket.EndConnect(ar);
#if DEBUG
                Log.WriteDebug($"Successfully initialized socket to {serverSocket.RemoteEndPoint}");
#endif
                OnConnected.Set();
            }

            catch (SocketException e)
            {
                throw new NetworkException($"Could not initialize socket for RCon - {e.Message}");
            }
        }

        private void OnSentCallback(IAsyncResult ar)
        {
            Socket serverConnection = (Socket)ar.AsyncState;

            try
            {
                int sentByteNum = serverConnection.EndSend(ar);
#if DEBUG
                Log.WriteDebug($"Sent {sentByteNum} bytes to {ServerConnection.RemoteEndPoint}");
#endif
                OnSent.Set();
            }

            catch (SocketException)
            {
            }
        }

        private void OnReceivedCallback(IAsyncResult ar)
        {
            var connectionState = (ConnectionState)ar.AsyncState;
            var serverConnection = connectionState.Client;

            try
            {
                int bytesRead = serverConnection.EndReceive(ar);

                if (bytesRead > 0)
                {
#if DEBUG
                    Log.WriteDebug($"Received {bytesRead} bytes from {ServerConnection.RemoteEndPoint}");
#endif
                    connectionState.ResponseString.Append(Encoding.UTF7.GetString(connectionState.Buffer, 0, bytesRead).TrimEnd('\0') + '\n');

                    if (!connectionState.Buffer.Take(4).ToArray().SequenceEqual(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }))
                        throw new NetworkException("Unexpected packet received");

                    if (serverConnection.Available > 0)
                    {
                        ServerConnection.BeginReceive(connectionState.Buffer, 0, connectionState.Buffer.Length, 0,
                            new AsyncCallback(OnReceivedCallback), connectionState);
                    }
                    else
                    {
                        response = connectionState.ResponseString.ToString();
                        OnReceived.Set();
                    }
                }
                else
                {
                    response = connectionState.ResponseString.ToString();
                    OnReceived.Set();
                }
            }

            catch (SocketException)
            {

            }
        }

        public async Task<string[]> SendQueryAsync(StaticHelpers.QueryType type, string parameters = "")
        {
            // will this really prevent flooding?
            if ((DateTime.Now - LastQuery).TotalMilliseconds < 35)
            {
                await Task.Delay(35);
            }

            LastQuery = DateTime.Now;

            OnSent.Reset();
            OnReceived.Reset();
            string queryString = "";

            switch (type)
            {
                case StaticHelpers.QueryType.DVAR:
                case StaticHelpers.QueryType.COMMAND:
                    queryString = $"ÿÿÿÿ\x02rcon {RConPassword} {parameters}";
                    break;
                case StaticHelpers.QueryType.GET_STATUS:
                    queryString = "ÿÿÿÿ\x02getstatus";
                    break;
            }

            byte[] payload = queryString.Select(Convert.ToByte).ToArray();

            retrySend:
            try
            {
                ServerConnection.BeginSend(payload, 0, payload.Length, 0, new AsyncCallback(OnSentCallback), ServerConnection);
                bool success = await Task.FromResult(OnSent.WaitOne(StaticHelpers.SocketTimeout));

                if (!success)
                {
                    FailedSends++;
#if DEBUG
                    Log.WriteDebug($"{FailedSends} failed sends to {ServerConnection.RemoteEndPoint.ToString()}");
#endif
                    if (FailedSends < 4)
                        goto retrySend;
                    else if (FailedSends == 4)
                        Log.WriteError($"Failed to send data to {ServerConnection.RemoteEndPoint}");
                }

                else
                {
                    if (FailedSends >= 4)
                    {
                        Log.WriteVerbose($"Resumed send RCon connection with {ServerConnection.RemoteEndPoint}");
                        FailedSends = 0;
                    }
                }
            }

            catch (SocketException e)
            {
                // this result is normal if the server is not listening
                if (e.NativeErrorCode != (int)SocketError.ConnectionReset &&
                   e.NativeErrorCode != (int)SocketError.TimedOut)
                    throw new NetworkException($"Unexpected error while sending data to server - {e.Message}");
            }

            var connectionState = new ConnectionState(ServerConnection);

            retryReceive:
            try
            {
                ServerConnection.BeginReceive(connectionState.Buffer, 0, connectionState.Buffer.Length, 0,
                    new AsyncCallback(OnReceivedCallback), connectionState);
                bool success = await Task.FromResult(OnReceived.WaitOne(StaticHelpers.SocketTimeout));

                if (!success)
                {
                    FailedReceives++;
#if DEBUG
                    Log.WriteDebug($"{FailedReceives} failed receives from {ServerConnection.RemoteEndPoint.ToString()}");
#endif
                    if (FailedReceives < 4)
                        goto retrySend;
                    else if (FailedReceives == 4)
                    {
                        Log.WriteError($"Failed to receive data from {ServerConnection.RemoteEndPoint} after {FailedReceives} tries");
                    }

                    if (FailedReceives >= 4)
                    {
                        throw new NetworkException($"Could not receive data from the {ServerConnection.RemoteEndPoint}");
                    }
                }

                else
                {
                    if (FailedReceives >= 4)
                    {
                        Log.WriteVerbose($"Resumed receive RCon connection from {ServerConnection.RemoteEndPoint}");
                        FailedReceives = 0;
                    }
                }
            }

            catch (SocketException e)
            {
                // this result is normal if the server is not listening
                if (e.NativeErrorCode != (int)SocketError.ConnectionReset &&
                    e.NativeErrorCode != (int)SocketError.TimedOut)
                    throw new NetworkException($"Unexpected error while receiving data from server - {e.Message}");
                else if (FailedReceives < 4)
                {
                    goto retryReceive;
                }

                else if (FailedReceives == 4)
                {
                    Log.WriteError($"Failed to receive data from {ServerConnection.RemoteEndPoint} after {FailedReceives} tries");
                }

                if (FailedReceives >= 4)
                {
                    throw new NetworkException(e.Message);
                }
            }

            string queryResponse = response;

            if (queryResponse.Contains("Invalid password"))
                throw new NetworkException("RCON password is invalid");
            if (queryResponse.ToString().Contains("rcon_password"))
                throw new NetworkException("RCON password has not been set");

            string[] splitResponse = queryResponse.Split(new char[]
            {
                '\n'
            }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()).ToArray();
            return splitResponse;
        }
    }
}
