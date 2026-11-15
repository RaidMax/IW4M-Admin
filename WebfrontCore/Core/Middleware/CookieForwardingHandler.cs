namespace WebfrontCore.Core.Middleware;

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

            // Tell the internal API who the real caller is. Only the address already resolved by the
            // forwarded-headers middleware is sent: relaying the caller's own X-Forwarded-For would let them
            // choose it, since this loopback self-call comes from a trusted proxy address.
            request.Headers.Remove("X-Forwarded-For");
            request.Headers.Add("X-Forwarded-For", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }

        return base.SendAsync(request, cancellationToken);
    }
}
