using System.Net;
using RconSharp;

namespace Integrations.Source.Interfaces
{
    public interface IRConClientFactory
    {
        RconClient CreateClient(IPEndPoint ipEndPoint);
    }
}