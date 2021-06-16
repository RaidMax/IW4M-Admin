using RconSharp;

namespace Integrations.Source.Interfaces
{
    public interface IRConClientFactory
    {
        RconClient CreateClient(string hostname, int port);
    }
}