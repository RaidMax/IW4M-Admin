using System.Net;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// defines the capabilities of an RCon connection factory
    /// </summary>
    public interface IRConConnectionFactory
    {
        /// <summary>
        /// creates an rcon connection instance
        /// </summary>
        /// <param name="ipEndpoint">ip address and port of the server</param>
        /// <param name="password"> password of the server</param>
        /// <param name="rconEngine">engine to create the rcon connection to</param>
        /// <returns>instance of rcon connection</returns>
        IRConConnection CreateConnection(IPEndPoint ipEndpoint, string password, string rconEngine);
    }
}
