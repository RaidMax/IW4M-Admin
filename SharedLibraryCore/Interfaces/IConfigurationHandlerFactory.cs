namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     defines the capabilities of the configuration handler factory
    ///     used to generate new instance of configuration handlers
    /// </summary>
    public interface IConfigurationHandlerFactory
    {
        /// <summary>
        ///     generates a new configuration handler
        /// </summary>
        /// <typeparam name="T">base configuration type</typeparam>
        /// <param name="name">file name of configuration</param>
        /// <returns>new configuration handler instance</returns>
        IConfigurationHandler<T> GetConfigurationHandler<T>(string name) where T : IBaseConfiguration;
    }
}