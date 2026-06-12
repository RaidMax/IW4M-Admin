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
    /// Raised when a caller opens a custom modal. The host's single modal host component subscribes
    /// and renders the request in host chrome (outside any plugin's scoped DOM), so the modal is
    /// styled by the host stylesheet rather than the plugin's scoped CSS.
    /// </summary>
    event Action<ModalRequest> OnOpenCustomAction;

    /// <summary>
    /// Open a modal rendering <see cref="ModalRequest.Content"/> as its body. See
    /// <see cref="ModalRequest"/> for the optional sizing and body-class overrides.
    /// </summary>
    void OpenCustom(ModalRequest request);
}
