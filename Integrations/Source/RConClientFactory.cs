using System.Net;
using Integrations.Source.Interfaces;
using RconSharp;

namespace Integrations.Source
{
    public class RConClientFactory : IRConClientFactory
    {
        public RconClient CreateClient(IPEndPoint ipEndPoint)
        {
            return RconClient.Create(ipEndPoint.Address.ToString(), ipEndPoint.Port);
        }
    }
}