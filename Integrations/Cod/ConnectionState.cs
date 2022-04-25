using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace Integrations.Cod
{
    /// <summary>
    /// used to keep track of the udp connection state
    /// </summary>
    internal class ConnectionState
    {
        ~ConnectionState()
        {
            OnComplete.Dispose();
            OnSentData.Dispose();
            OnReceivedData.Dispose();
        }

        public int ConnectionAttempts { get; set; }
        private const int BufferSize = 16384;
        public readonly byte[] ReceiveBuffer = new byte[BufferSize];
        public readonly SemaphoreSlim OnComplete = new(1, 1);
        public readonly SemaphoreSlim OnSentData = new(1, 1);
        public readonly SemaphoreSlim OnReceivedData = new (1, 1);
       
        public List<int> BytesReadPerSegment { get; set; } = new();
        public SocketAsyncEventArgs SendEventArgs { get; set; } = new();
        public SocketAsyncEventArgs ReceiveEventArgs { get; set; } = new();
        public DateTime LastQuery { get; set; } = DateTime.Now;
    }

    internal class ConnectionUserToken
    {
        public Socket Socket { get; set; }
        public CancellationToken CancellationToken { get; set; }
    }
}
