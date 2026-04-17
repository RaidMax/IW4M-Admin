using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace WebfrontCore.Core.OpenApi;

/// <summary>
/// Rewrites the OpenAPI document's <c>servers</c> entry to match the public URL the
/// browser actually used. Without this, Scalar's "Test Request" runner hits the
/// Kestrel bind URL (typically http://) and a deployment fronted by Cloudflare/nginx
/// with TLS termination triggers a mixed-content block when the page itself is
/// served over HTTPS.
///
/// Resolution order:
/// 1. <c>X-Forwarded-Proto</c> / <c>X-Forwarded-Host</c> (populated by UseForwardedHeaders
///    when the upstream proxy is configured to forward them).
/// 2. <c>CF-Visitor</c> (Cloudflare's JSON scheme hint — `{"scheme":"https"}`). Many
///    nginx configs don't forward X-Forwarded-Proto, but Cloudflare's header arrives
///    untouched.
/// 3. <c>Referer</c> / <c>Origin</c> — Scalar fetches /openapi/*.json from the
///    rendered /api-docs page, so the Referer carries the real public scheme/host.
/// 4. <c>Request.Scheme</c> / <c>Request.Host</c> as a last resort.
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

        var (scheme, host) = ResolvePublicOrigin(request);

        if (string.IsNullOrEmpty(scheme) || string.IsNullOrEmpty(host))
        {
            return Task.CompletedTask;
        }

        document.Servers = [new OpenApiServer { Url = $"{scheme}://{host}" }];
        return Task.CompletedTask;
    }

    private static (string? Scheme, string? Host) ResolvePublicOrigin(HttpRequest request)
    {
        var scheme = FirstHeaderValue(request, "X-Forwarded-Proto");
        var host = FirstHeaderValue(request, "X-Forwarded-Host");

        // Cloudflare hint — overrides scheme if we still have http. Format: {"scheme":"https"}
        if (!string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase)
            && request.Headers.TryGetValue("CF-Visitor", out var cfVisitor)
            && cfVisitor.Count > 0
            && cfVisitor.ToString().Contains("\"https\"", StringComparison.OrdinalIgnoreCase))
        {
            scheme = "https";
        }

        // Referer / Origin carries the browser's view of the URL — last reliable hop
        // before falling back to the local request scheme.
        if (string.IsNullOrEmpty(scheme) || string.IsNullOrEmpty(host))
        {
            var refererRaw = request.Headers.Referer.ToString();
            if (string.IsNullOrEmpty(refererRaw))
            {
                refererRaw = request.Headers.Origin.ToString();
            }

            if (!string.IsNullOrEmpty(refererRaw)
                && Uri.TryCreate(refererRaw, UriKind.Absolute, out var referer))
            {
                if (string.IsNullOrEmpty(scheme)) scheme = referer.Scheme;
                if (string.IsNullOrEmpty(host)) host = referer.Authority;
            }
        }

        scheme ??= request.Scheme;
        if (string.IsNullOrEmpty(host)) host = request.Host.Value;

        return (scheme, host);
    }

    private static string? FirstHeaderValue(HttpRequest request, string headerName)
    {
        if (!request.Headers.TryGetValue(headerName, out var values) || values.Count == 0)
        {
            return null;
        }

        var raw = values.ToString();
        if (string.IsNullOrEmpty(raw)) return null;

        // Proxy chains comma-separate multiple values — take the first.
        var first = raw.Split(',')[0].Trim();
        return string.IsNullOrEmpty(first) ? null : first;
    }
}
