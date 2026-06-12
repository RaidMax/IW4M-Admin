namespace WebCommon.Services;

/// <summary>
/// Fire-and-forget notification surface. The host implements this; plugins inject
/// <see cref="IToastService"/> and call the Show* helpers without referencing host types.
/// The host mounts a single <c>ToastContainer</c> that subscribes to <see cref="OnShow"/>.
/// </summary>
public interface IToastService
{
    event Action<ToastMessage> OnShow;
    Task ShowSuccessAsync(string message, string? title = null, int? duration = null);
    Task ShowErrorAsync(string message, string? title = null, int? duration = null);
    Task ShowWarningAsync(string message, string? title = null, int? duration = null);
    Task ShowInfoAsync(string message, string? title = null, int? duration = null);
}
