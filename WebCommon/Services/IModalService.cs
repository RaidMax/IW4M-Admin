using Microsoft.AspNetCore.Components;

namespace WebCommon.Services;

/// <summary>
/// Minimal modal surface a plugin can depend on to open a custom-content modal without
/// referencing the host. The host's action service implements this; plugins inject
/// <see cref="IModalService"/> and call <see cref="OpenCustom"/>. Kept deliberately small
/// (just custom content) so it carries no host-only types.
/// </summary>
public interface IModalService
{
    /// <summary>
    /// Raised when a caller opens a custom modal. The host's modal host component subscribes
    /// and renders <paramref name="content"/>.
    /// </summary>
    event Action<RenderFragment, string, string?, string?> OnOpenCustomAction;

    /// <summary>
    /// Open a modal with a caller-supplied <see cref="RenderFragment"/> as the body.
    /// </summary>
    /// <param name="bodyClass">
    /// Optional override for the modal body's classes. Default ("p-6 overflow-y-auto") suits
    /// forms and simple lists. Custom content that manages its own layout should pass a class
    /// like "flex-1 min-h-0 overflow-hidden flex flex-col" so the body fills remaining vertical
    /// space without adding its own scrollbar.
    /// </param>
    void OpenCustom(RenderFragment content, string title, string? modalClass = null, string? bodyClass = null);
}
