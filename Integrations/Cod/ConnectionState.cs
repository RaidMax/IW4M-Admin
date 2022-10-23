using System;
using System.Collections.Generic;
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
        }

        public int ConnectionAttempts { get; set; }
        private const int BufferSize = 16384;
        public readonly byte[] ReceiveBuffer = new byte[BufferSize];
        public readonly SemaphoreSlim OnComplete = new(1, 1);
        public List<byte[]> ReceivedBytes { get; } = new();
        public DateTime LastQuery { get; set; } = DateTime.Now;
    }
}
