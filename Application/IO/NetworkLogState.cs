using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace IW4MAdmin.Application.IO;

public class NetworkLogState : Dictionary<IPEndPoint, UdpClientState>
{
    public UdpClientState RegisterEndpoint(IPEndPoint serverEndpoint, IPEndPoint localEndpoint)
    {
        try
        {
            lock (this)
            {
                if (!ContainsKey(serverEndpoint))
                {
                    Add(serverEndpoint, new UdpClientState { Client = new UdpClient(localEndpoint) });
                }
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            lock (this)
            {
                // we don't add the udp client because it already exists (listening to multiple servers from one socket)
                Add(serverEndpoint,  new UdpClientState());
            }
        }

        return this[serverEndpoint];
    }


    public List<byte[]> GetServerLogData(IPEndPoint serverEndpoint)
    {
        try
        {
            var state = this[serverEndpoint];
            
            if (state == null)
            {
                return new List<byte[]>();
            }
            
            // it's possible that we could be trying to read and write to the queue simultaneously so we need to wait 
            this[serverEndpoint].OnAction.Wait();

            var data = new List<byte[]>();
            
            while (this[serverEndpoint].AvailableLogData.Count > 0)
            {
                data.Add(this[serverEndpoint].AvailableLogData.Dequeue());
            }
           
            return data;
        }
        finally
        {
            if (this[serverEndpoint].OnAction.CurrentCount == 0)
            {
                this[serverEndpoint].OnAction.Release(1);
            }
        }
    }

    public void QueueServerLogData(IPEndPoint serverEndpoint, byte[] data)
    {
        var endpoint = Keys.FirstOrDefault(key =>
            Equals(key.Address, serverEndpoint.Address) && key.Port == serverEndpoint.Port);

        try
        {
            if (endpoint == null)
            {
                return;
            }

            // currently our expected start and end characters
            var startsWithPrefix = StartsWith(data, "ÿÿÿÿprint\n");
            var endsWithDelimiter = data[^1] == '\n';

            // we have the data we expected
            if (!startsWithPrefix || !endsWithDelimiter)
            {
                return;
            }

            // it's possible that we could be trying to read and write to the queue simultaneously so we need to wait 
            this[endpoint].OnAction.Wait();
            this[endpoint].AvailableLogData.Enqueue(data);
        }
        finally
        {
            if (endpoint != null && this[endpoint].OnAction.CurrentCount == 0)
            {
                this[endpoint].OnAction.Release(1);
            }
        }
    }

    public bool EndPointExists(IPEndPoint serverEndpoint)
    {
        lock (this)
        {
            return ContainsKey(serverEndpoint);
        }
    }

    private static bool StartsWith(byte[] sourceArray, string match)
    {
        if (sourceArray is null)
        {
            return false;
        }
        
        if (match.Length > sourceArray.Length)
        {
            return false;
        }

        return !match.Where((t, i) => sourceArray[i] != (byte)t).Any();
    }
}

public class UdpClientState
{
    public UdpClient Client { get; set; }
    public Queue<byte[]> AvailableLogData { get; } = new();
    public SemaphoreSlim OnAction { get; } = new(1, 1);

    ~UdpClientState()
    {
        OnAction.Dispose();
        Client?.Dispose();
    }
}
