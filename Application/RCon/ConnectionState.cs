using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace IW4MAdmin.Application.RCon
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
        public readonly SemaphoreSlim OnComplete = new SemaphoreSlim(1, 1);
        public readonly ManualResetEventSlim OnSentData = new ManualResetEventSlim(false);
        public readonly ManualResetEventSlim OnReceivedData = new ManualResetEventSlim(false);
        public List<int> BytesReadPerSegment { get; set; } = new List<int>();
        public SocketAsyncEventArgs SendEventArgs { get; set; } = new SocketAsyncEventArgs();
        public SocketAsyncEventArgs ReceiveEventArgs { get; set; } = new SocketAsyncEventArgs();
        public DateTime LastQuery { get; set; } = DateTime.Now;
    }
}
