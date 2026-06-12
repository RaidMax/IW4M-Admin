using Microsoft.AspNetCore.Components;

namespace WebCommon.Services;

/// <summary>
/// A request to open a custom-content modal via <see cref="IModalService.OpenCustom"/>.
/// <see cref="Content"/> and <see cref="Title"/> are required positional values; the optional
/// sizing/body classes are named init properties so they can't be transposed by mistake.
/// </summary>
/// <param name="Content">The modal body, supplied by the caller.</param>
/// <param name="Title">The modal header title.</param>
public sealed record ModalRequest(RenderFragment Content, string Title)
{
    /// <summary>
    /// Optional override for the modal shell's sizing. This is the COMPLETE size set — width AND
    /// max-height (default "max-w-lg max-h-[90vh]") — not an addition to a baked-in default, so
    /// include a max-h-* or the modal can outgrow the viewport. These classes land on host-rendered
    /// DOM (outside any plugin's scoped CSS), so they must exist in the HOST stylesheet — use the
    /// modal sizing vocabulary safelisted in WebfrontCore/wwwroot/css/src/app.css.
    /// </summary>
    public string? ModalClass { get; init; }

    /// <summary>
    /// Optional override for the modal body's classes. Default ("p-6 overflow-y-auto") suits forms
    /// and simple lists. Custom content that manages its own layout should pass something like
    /// "flex-1 min-h-0 overflow-hidden flex flex-col" so the body fills the remaining vertical space
    /// without adding its own scrollbar. Same host-stylesheet rule as <see cref="ModalClass"/>.
    /// </summary>
    public string? BodyClass { get; init; }
}
