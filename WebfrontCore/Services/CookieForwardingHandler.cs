using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WebfrontCore.Services
{
    /// <summary>
    /// Forwards cookies from the current HTTP request to outgoing API calls
    /// This ensures that authentication is preserved when Blazor calls internal APIs
    /// </summary>
    public class CookieForwardingHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CookieForwardingHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                // Forward all cookies from the incoming request to the outgoing API call
                var cookies = httpContext.Request.Headers["Cookie"].ToString();
                if (!string.IsNullOrEmpty(cookies))
                {
                    request.Headers.Add("Cookie", cookies);
                }

                // Forward X-Forwarded-For to preserve client IP and prevent localhost auto-auth
                // If no X-Forwarded-For exists, use the original remote IP
                var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
                if (string.IsNullOrEmpty(forwardedFor))
                {
                    var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    request.Headers.Add("X-Forwarded-For", remoteIp);
                }
                else
                {
                    request.Headers.Add("X-Forwarded-For", forwardedFor);
                }
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
