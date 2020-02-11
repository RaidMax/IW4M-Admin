using SharedLibraryCore.Configuration;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// defines the capabilities of game server instance factory
    /// </summary>
    public interface IGameServerInstanceFactory
    {
        /// <summary>
        /// creates the instance of a game server
        /// </summary>
        /// <param name="config">server configuration</param>
        /// <param name="manager">application manager</param>
        /// <returns></returns>
        Server CreateServer(ServerConfiguration config, IManager manager);
    }
}
