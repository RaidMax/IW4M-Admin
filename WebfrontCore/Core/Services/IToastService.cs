
namespace WebfrontCore.Core.Services;

public interface IToastService
{
    event Action<ToastMessage> OnShow;
    Task ShowSuccessAsync(string message, string title = null, int? duration = null);
    Task ShowErrorAsync(string message, string title = null, int? duration = null);
    Task ShowWarningAsync(string message, string title = null, int? duration = null);
    Task ShowInfoAsync(string message, string title = null, int? duration = null);
}
