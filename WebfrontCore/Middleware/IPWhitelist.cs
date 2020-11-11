using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace WebfrontCore.Middleware
{
    /// <summary>
    /// Defines the middleware functioning to whitelist connection from 
    /// a set of IP Addresses
    /// </summary>
    internal sealed class IPWhitelist
    {
        private readonly byte[][] _whitelistedIps;
        private readonly RequestDelegate _nextRequest;
        private readonly ILogger _logger;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="nextRequest"></param>
        /// <param name="whitelistedIps">list of textual ip addresses</param>
        public IPWhitelist(RequestDelegate nextRequest, ILogger<IPWhitelist> logger, string[] whitelistedIps)
        {
            _whitelistedIps = whitelistedIps.Select(_ip => System.Net.IPAddress.Parse(_ip).GetAddressBytes()).ToArray();
            _nextRequest = nextRequest;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var isAllowed = true;

            if (_whitelistedIps.Length > 0)
            {
                isAllowed = _whitelistedIps.Any(_ip => _ip.SequenceEqual(context.Connection.RemoteIpAddress.GetAddressBytes()));
            }

            if (isAllowed)
            {
                await _nextRequest.Invoke(context);
            }

            else
            {
                _logger.LogDebug("Blocking HTTP request from {ipAddress}", context.Connection.RemoteIpAddress);
                context.Abort();
            }
        }
    }
}
