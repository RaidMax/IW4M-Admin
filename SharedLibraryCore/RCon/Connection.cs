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
        public IPEndPoint Endpoint { get; private set; }
        public string RConPassword { get; private set; }
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
                Log.WriteDebug($"Sent {sentByteNum} bytes to {serverConnection.RemoteEndPoint}");
#endif
                // this is where we override our await to make it 
                OnSent.Set();
            }

            catch (Exception)
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
                    Log.WriteDebug($"Received {bytesRead} bytes from {serverConnection.RemoteEndPoint}");
#endif
                    FailedReceives = 0;
                    connectionState.ResponseString.Append(Utilities.EncodingType.GetString(connectionState.Buffer, 0, bytesRead).TrimEnd('\0') + '\n');

                    if (!connectionState.Buffer.Take(4).ToArray().SequenceEqual(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }))
                        throw new NetworkException("Unexpected packet received");

                    /* if (FailedReceives == 0 && serverConnection.Available > 0)
                     {
                         serverConnection.BeginReceive(connectionState.Buffer, 0, connectionState.Buffer.Length, 0,
                             new AsyncCallback(OnReceivedCallback), connectionState);
                     }
                     else*/
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

            catch (ObjectDisposedException)
            {
                // Log.WriteWarning($"Tried to check for more available bytes for disposed socket on {Endpoint}");
            }
        }

        public async Task<string[]> SendQueryAsync(StaticHelpers.QueryType type, string parameters = "", bool waitForResponse = true)
        {
            // will this really prevent flooding?
            if ((DateTime.Now - LastQuery).TotalMilliseconds < 350)
            {
                await Task.Delay(350);
            }

            LastQuery = DateTime.Now;

            OnSent.Reset();
            OnReceived.Reset();
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

            using (var socketConnection = new Socket(Endpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
            {
                socketConnection.BeginConnect(Endpoint, new AsyncCallback(OnConnectedCallback), socketConnection);

                retrySend:
                try
                {
#if DEBUG
                    Console.WriteLine($"Sending Command {parameters}");
#endif
                    if (!OnConnected.WaitOne(StaticHelpers.SocketTimeout))
                        throw new SocketException((int)SocketError.TimedOut);

                    socketConnection.BeginSend(payload, 0, payload.Length, 0, new AsyncCallback(OnSentCallback), socketConnection);
                    bool success = await Task.FromResult(OnSent.WaitOne(StaticHelpers.SocketTimeout));

                    if (!success)
                    {
                        FailedSends++;
#if DEBUG
                        Log.WriteDebug($"{FailedSends} failed sends to {socketConnection.RemoteEndPoint.ToString()}");
#endif
                        if (FailedSends < 4)
                            goto retrySend;
                        else if (FailedSends == 4)
                            Log.WriteError($"Failed to send data to {socketConnection.RemoteEndPoint}");
                    }

                    else
                    {
                        if (FailedSends >= 4)
                        {
                            Log.WriteVerbose($"Resumed send RCon connection with {socketConnection.RemoteEndPoint}");
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

                if (!waitForResponse)
                    return await Task.FromResult(new string[] { "" });

                var connectionState = new ConnectionState(socketConnection);

                retryReceive:
                try
                {
                    socketConnection.BeginReceive(connectionState.Buffer, 0, connectionState.Buffer.Length, 0,
                        new AsyncCallback(OnReceivedCallback), connectionState);
                    bool success = await Task.FromResult(OnReceived.WaitOne(StaticHelpers.SocketTimeout));

                    if (!success)
                    {

                        FailedReceives++;
#if DEBUG
                        Log.WriteDebug($"{FailedReceives} failed receives from {socketConnection.RemoteEndPoint.ToString()}");
#endif
                        if (FailedReceives < 4)
                            goto retrySend;
                        else if (FailedReceives == 4)
                        {
                          //  Log.WriteError($"Failed to receive data from {socketConnection.RemoteEndPoint} after {FailedReceives} tries");
                        }

                        if (FailedReceives >= 4)
                        {
                            throw new NetworkException($"{Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_COMMUNICATION"]} {socketConnection.RemoteEndPoint.ToString()}");
                        }
                    }

                    else
                    {
                        if (FailedReceives >= 4)
                        {
                            Log.WriteVerbose($"Resumed receive RCon connection from {socketConnection.RemoteEndPoint.ToString()}");
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
                       // Log.WriteError($"Failed to receive data from {socketConnection.RemoteEndPoint} after {FailedReceives} tries");
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
}
