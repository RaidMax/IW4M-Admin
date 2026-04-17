using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace WebfrontCore.Core.OpenApi;

/// <summary>
/// Rewrites the OpenAPI document's <c>servers</c> entry to match the current request's
/// public scheme and host. Without this, Scalar uses the OpenAPI default (derived from
/// the Kestrel bind URL — typically http://) and a deployment fronted by Cloudflare/nginx
/// with TLS termination produces <c>http://</c> request URLs that browsers block as
/// mixed content when the page itself is served over HTTPS.
/// </summary>
internal sealed class ServerUrlTransformer(IHttpContextAccessor httpContextAccessor) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var request = httpContextAccessor.HttpContext?.Request;
        if (request is null)
        {
            return Task.CompletedTask;
        }

        // Prefer X-Forwarded-* when present — UseForwardedHeaders populates Scheme/Host
        // from them, but fall back to the raw headers in case the middleware isn't wired.
        var scheme = request.Headers.TryGetValue("X-Forwarded-Proto", out var proto) && proto.Count > 0
            ? proto.ToString().Split(',')[0].Trim()
            : request.Scheme;

        var host = request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHost) && forwardedHost.Count > 0
            ? forwardedHost.ToString().Split(',')[0].Trim()
            : request.Host.Value;

        if (string.IsNullOrEmpty(scheme) || string.IsNullOrEmpty(host))
        {
            return Task.CompletedTask;
        }

        var url = $"{scheme}://{host}";
        document.Servers = [new OpenApiServer { Url = url }];

        return Task.CompletedTask;
    }
}
