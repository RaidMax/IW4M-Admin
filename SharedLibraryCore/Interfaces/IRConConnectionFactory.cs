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
        /// <param name="ipAddress">ip address of the server</param>
        /// <param name="port">port of the server</param>
        /// <param name="password"> password of the server</param>
        /// <returns>instance of rcon connection</returns>
        IRConConnection CreateConnection(string ipAddress, int port, string password);
    }
}
