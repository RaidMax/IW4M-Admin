using IW4MAdmin.Application.Misc;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Factories
{
    /// <summary>
    /// implementation of IConfigurationHandlerFactory
    /// provides base functionality to create configuration handlers
    /// </summary>
    public class ConfigurationHandlerFactory : IConfigurationHandlerFactory
    {
        /// <summary>
        /// creates a base configuration handler
        /// </summary>
        /// <typeparam name="T">base configuration type</typeparam>
        /// <param name="name">name of the config file</param>
        /// <returns></returns>
        public IConfigurationHandler<T> GetConfigurationHandler<T>(string name) where T : IBaseConfiguration
        {
            return new BaseConfigurationHandler<T>(name);
        }
    }
}
