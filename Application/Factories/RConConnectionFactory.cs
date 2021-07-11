using System;
using System.Net;
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

        /// <inheritdoc/>
        public IRConConnection CreateConnection(IPEndPoint ipEndpoint, string password, string rconEngine)
        {
            return rconEngine switch
            {
                "COD" => new CodRConConnection(ipEndpoint, password,
                    _serviceProvider.GetRequiredService<ILogger<CodRConConnection>>(), GameEncoding),
                "Source"  => new SourceRConConnection(_serviceProvider.GetRequiredService<ILogger<SourceRConConnection>>(),
                    _serviceProvider.GetRequiredService<IRConClientFactory>(), ipEndpoint, password),
                _ => throw new ArgumentException($"No supported RCon engine available for '{rconEngine}'")
            };
        }
    }
}