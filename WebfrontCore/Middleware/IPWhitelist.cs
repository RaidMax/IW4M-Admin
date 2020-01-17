using Microsoft.AspNetCore.Http;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        public IPWhitelist(RequestDelegate nextRequest, ILogger logger, string[] whitelistedIps)
        {
            _whitelistedIps = whitelistedIps.Select(_ip => System.Net.IPAddress.Parse(_ip).GetAddressBytes()).ToArray();
            _nextRequest = nextRequest;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            bool isAlllowed = true;

            if (_whitelistedIps.Length > 0)
            {
                isAlllowed = _whitelistedIps.Any(_ip => _ip.SequenceEqual(context.Connection.RemoteIpAddress.GetAddressBytes()));
            }

            if (isAlllowed)
            {
                await _nextRequest.Invoke(context);
            }

            else
            {
                _logger.WriteInfo($"Blocking HTTP request from {context.Connection.RemoteIpAddress.ToString()}");
                context.Abort();
            }
        }
    }
}
