using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace WebCommon.Components.UI;

/// <summary>
/// An <see cref="ErrorBoundary"/> that logs the exceptions it catches, tagged with a caller-supplied
/// <see cref="Context"/> string, before falling back to its <c>ErrorContent</c>.
///
/// Its purpose is diagnostic attribution. A component that throws while being instantiated or rendered
/// inside the Blazor render tree — for example a plugin-provided slot widget whose injected services
/// are resolved from a circuit scope that is tearing down — otherwise surfaces only as an unobserved
/// framework task exception whose stack names no concrete component. Wrapping the render in this
/// boundary routes the failure here, where the offending component's identity (passed in via
/// <see cref="Context"/>) can be written to the log, and contains it so it neither bubbles to the host
/// nor escapes as unobserved noise.
/// </summary>
public sealed class LoggingErrorBoundary : ErrorBoundary
{
    [Inject] private ILogger<LoggingErrorBoundary> Logger { get; set; } = default!;

    /// <summary>Free-text tag identifying what is being rendered (e.g. slot name + component type + plugin id).</summary>
    [Parameter] public string? Context { get; set; }

    protected override async Task OnErrorAsync(Exception exception)
    {
        Logger.LogWarning(exception,
            "Render failure contained by error boundary{Context}",
            string.IsNullOrEmpty(Context) ? string.Empty : $" [{Context}]");

        // Defer to the base so the boundary latches the error and renders ErrorContent. We do NOT
        // auto-recover: re-running the same child that just failed would risk a render loop on a
        // live circuit, and on a tearing-down circuit there is nothing left to recover.
        await base.OnErrorAsync(exception);
    }
}
