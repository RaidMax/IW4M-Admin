using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
        private readonly List<byte[]> whitelistedIps;
        private readonly RequestDelegate nextRequest;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="nextRequest"></param>
        /// <param name="logger"></param>
        /// <param name="whitelistedIps">list of textual ip addresses</param>
        public IPWhitelist(RequestDelegate nextRequest, ILogger<IPWhitelist> logger, List<string> whitelistedIps)
        {
            this.whitelistedIps = whitelistedIps.Select(_ip => System.Net.IPAddress.Parse(_ip).GetAddressBytes()).ToList();
            this.nextRequest = nextRequest;
        }

        public async Task Invoke(HttpContext context)
        {
            bool isAlllowed = whitelistedIps.Any(_ip => _ip.SequenceEqual(context.Connection.RemoteIpAddress.GetAddressBytes()));

            if (isAlllowed)
            {
                await nextRequest.Invoke(context);
            }

            else
            {
                context.Abort();
            }
        }
    }
}
