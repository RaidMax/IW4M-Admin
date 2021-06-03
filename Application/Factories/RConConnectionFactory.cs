using System;
using SharedLibraryCore.Interfaces;
using System.Text;
using Integrations.Cod;
using Integrations.Source;
using Integrations.Source.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IW4MAdmin.Application.Factories
{
    /// <summary>
    /// implementation of IRConConnectionFactory
    /// </summary>
    internal class RConConnectionFactory : IRConConnectionFactory
    {
        private static readonly Encoding GameEncoding = Encoding.GetEncoding("windows-1252");
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Base constructor
        /// </summary>
        /// <param name="logger"></param>
        public RConConnectionFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// creates a new rcon connection instance
        /// </summary>
        /// <param name="ipAddress">ip address of the server</param>
        /// <param name="port">port of the server</param>
        /// <param name="password">rcon password of the server</param>
        /// <returns></returns>
        public IRConConnection CreateConnection(string ipAddress, int port, string password, string rconEngine)
        {
            return rconEngine switch
            {
                "COD" => new CodRConConnection(ipAddress, port, password,
                    _serviceProvider.GetRequiredService<ILogger<CodRConConnection>>(), GameEncoding),
                "Source"  => new SourceRConConnection(_serviceProvider.GetRequiredService<ILogger<SourceRConConnection>>(),
                    _serviceProvider.GetRequiredService<IRConClientFactory>(), ipAddress, port, password),
                _ => throw new ArgumentException($"No supported RCon engine available for '{rconEngine}'")
            };
        }
    }
}