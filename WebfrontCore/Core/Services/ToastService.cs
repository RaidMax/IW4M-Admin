namespace WebfrontCore.Core.Services;

public class ToastService : IToastService
{
    private readonly AppState _appState;
    public event Action<ToastMessage> OnShow;

    public ToastService(AppState appState)
    {
        _appState = appState;
    }

    public Task ShowSuccessAsync(string message, string title = null, int? duration = null)
    {
        title ??= _appState.Loc("WEBFRONT_SCRIPT_ACTION_SUCCESS");
        ShowToast(message, title, ToastType.Success, duration);
        return Task.CompletedTask;
    }

    public Task ShowErrorAsync(string message, string title = null, int? duration = null)
    {
        title ??= "Error";
        ShowToast(message, title, ToastType.Error, duration);
        return Task.CompletedTask;
    }

    public Task ShowWarningAsync(string message, string title = null, int? duration = null)
    {
        title ??= "Warning";
        ShowToast(message, title, ToastType.Warning, duration);
        return Task.CompletedTask;
    }

    public Task ShowInfoAsync(string message, string title = null, int? duration = null)
    {
        title ??= "Info";
        ShowToast(message, title, ToastType.Info, duration);
        return Task.CompletedTask;
    }

    private void ShowToast(string content, string title, ToastType type, int? duration)
    {
        var toast = new ToastMessage
        {
            Title = title,
            Message = content,
            Type = type,
            Duration = duration ?? 5000
        };

        OnShow?.Invoke(toast);
    }
}
