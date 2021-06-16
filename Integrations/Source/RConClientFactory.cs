using Integrations.Source.Interfaces;
using RconSharp;

namespace Integrations.Source
{
    public class RConClientFactory : IRConClientFactory
    {
        public RconClient CreateClient(string hostname, int port)
        {
            return RconClient.Create(hostname, port);
        }
    }
}