using IW4MAdmin.Application.RCon;
using SharedLibraryCore.Interfaces;
using System.Text;
using Microsoft.Extensions.Logging;

namespace IW4MAdmin.Application.Factories
{
    /// <summary>
    /// implementation of IRConConnectionFactory
    /// </summary>
    internal class RConConnectionFactory : IRConConnectionFactory
    {
        private static readonly Encoding gameEncoding = Encoding.GetEncoding("windows-1252");
        private readonly ILogger<RConConnection> _logger;
       
        /// <summary>
        /// Base constructor
        /// </summary>
        /// <param name="logger"></param>
        public RConConnectionFactory(ILogger<RConConnection> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// creates a new rcon connection instance
        /// </summary>
        /// <param name="ipAddress">ip address of the server</param>
        /// <param name="port">port of the server</param>
        /// <param name="password">rcon password of the server</param>
        /// <returns></returns>
        public IRConConnection CreateConnection(string ipAddress, int port, string password)
        {
            return new RConConnection(ipAddress, port, password, _logger, gameEncoding);
        }
    }
}
