using SharedLibrary.Exceptions;
using SharedLibrary.Interfaces;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharedLibrary.RCon
{
    class ConnectionState
    {
        public Socket Client { get; set; }
        public const int BufferSize = 8192;
        public byte[] Buffer = new byte[BufferSize];
        public readonly StringBuilder ResponseString = new StringBuilder();
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
                FailedSends = 0;
                OnSent.Set();
            }

            catch (Exception e)
            {
                FailedSends++;
                Log.WriteWarning($"Could not send RCon data to server - {e.Message}");
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
                    connectionState.ResponseString.Append(Encoding.UTF7.GetString(connectionState.Buffer, 0, bytesRead).TrimEnd('\0'));

                    if (serverConnection.Available > 0)
                    {
                        ServerConnection.BeginReceive(connectionState.Buffer, 0, connectionState.Buffer.Length, 0,
                            new AsyncCallback(OnReceivedCallback), connectionState);
                    }
                    else
                    {
                        FailedReceives = 0;
                        OnReceived.Set();
                    }
                }
                else
                {
                    OnReceived.Set();
                }
            }

            catch (Exception)
            {
                FailedReceives++;
            }
        }

        public async Task<string[]> SendQueryAsync(StaticHelpers.QueryType type, string parameters = "")
        {
            // will this really prevent flooding?
            if ((DateTime.Now - LastQuery).TotalMilliseconds < 300)
            {
                await Task.Delay(300);
                LastQuery = DateTime.Now;
            }

            OnSent.Reset();
            OnReceived.Reset();
            string queryString = "";

            switch (type)
            {
                case StaticHelpers.QueryType.DVAR:
                case StaticHelpers.QueryType.COMMAND:
                    queryString = $"ÿÿÿÿrcon {RConPassword} {parameters}";
                    break;
                case StaticHelpers.QueryType.GET_STATUS:
                    queryString = "ÿÿÿÿgetstatus";
                    break;
            }

            byte[] payload = Encoding.Default.GetBytes(queryString);

            try
            {
                retrySend:
                ServerConnection.BeginSend(payload, 0, payload.Length, 0, new AsyncCallback(OnSentCallback), ServerConnection);
                bool success = await Task.FromResult(OnSent.WaitOne(StaticHelpers.SocketTimeout));

                if (!success)
                {
#if DEBUG
                    Log.WriteDebug($"{FailedSends} failed sends to {ServerConnection.RemoteEndPoint.ToString()}");
#endif
                    if (FailedSends < 4)
                        goto retrySend;
                    else
                        throw new NetworkException($"Could not send data to server - {new SocketException((int)SocketError.TimedOut).Message}");
                }
            }

            catch (SocketException e)
            {
                // this result is normal if the server is not listening
                if (e.HResult != (int)SocketError.ConnectionReset)
                    throw new NetworkException($"Unexpected error while sending data to server - {e.Message}");
            }

            var connectionState = new ConnectionState
            {
                Client = ServerConnection
            };

            try
            {
                retryReceive:
                ServerConnection.BeginReceive(connectionState.Buffer, 0, connectionState.Buffer.Length, 0,
                    new AsyncCallback(OnReceivedCallback), connectionState);
                bool success = await Task.FromResult(OnReceived.WaitOne(StaticHelpers.SocketTimeout));

                if (!success)
                {
#if DEBUG
                    Log.WriteDebug($"{FailedReceives} failed receives from {ServerConnection.RemoteEndPoint.ToString()}");
#endif
                    FailedReceives++;
                    if (FailedReceives < 4)
                        goto retryReceive;
                    else if (FailedReceives == 4)
                        Log.WriteError($"Failed to receive data from {ServerConnection.RemoteEndPoint}");
                    else
                        throw new NetworkException($"Could not receive data from the server - {new SocketException((int)SocketError.TimedOut).Message}");
                }
            }

            catch (SocketException e)
            {
                // this result is normal if the server is not listening
                if (e.HResult != (int)SocketError.ConnectionReset)
                    throw new NetworkException($"Unexpected error while receiving data from server - {e.Message}");
            }

            string queryResponse = connectionState.ResponseString.ToString();

            if (queryResponse.Contains("Invalid password"))
                throw new NetworkException("RCON password is invalid");
            if (queryResponse.ToString().Contains("rcon_password"))
                throw new NetworkException("RCON password has not been set");

            string[] splitResponse = queryResponse.Split(new char[]
            {
                StaticHelpers.SeperatorChar
            }, StringSplitOptions.RemoveEmptyEntries);
            return splitResponse;
        }
    }
}
