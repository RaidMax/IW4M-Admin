using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace WebfrontCore.Core.OpenApi;

/// <summary>
/// Forces the OpenAPI <c>servers</c> entry to a relative URL. The default entry built
/// from the request scheme/host hard-codes <c>http://</c> when Kestrel sits behind a
/// TLS-terminating proxy (Cloudflare/nginx), and Scalar's "Test Request" runner then
/// triggers mixed-content blocks on an HTTPS page. A relative URL is resolved by the
/// browser against the page origin, so it always matches the scheme the page loaded
/// over — same mechanism Blazor's NavigationManager relies on for relative hrefs.
/// </summary>
internal sealed class ServerUrlTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Servers = [new OpenApiServer { Url = "/" }];
        return Task.CompletedTask;
    }
}
