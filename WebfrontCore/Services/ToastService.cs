using Microsoft.JSInterop;

namespace WebfrontCore.Services
{
    public class ToastService : IToastService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly AppState _appState;

        public ToastService(IJSRuntime jsRuntime, AppState appState)
        {
            _jsRuntime = jsRuntime;
            _appState = appState;
        }

        public async Task ShowSuccessAsync(string message, string title = null, int? duration = null)
        {
            title ??= _appState.Loc("WEBFRONT_SCRIPT_ACTION_SUCCESS");
            await ShowToastAsync(message, title, "alert-success", "filled", duration);
        }

        public async Task ShowErrorAsync(string message, string title = null, int? duration = null)
        {
            title ??= "Error";
            await ShowToastAsync(message, title, "alert-danger", "filled", duration);
        }

        public async Task ShowWarningAsync(string message, string title = null, int? duration = null)
        {
            title ??= "Warning";
            await ShowToastAsync(message, title, "alert-warning", "filled", duration);
        }

        public async Task ShowInfoAsync(string message, string title = null, int? duration = null)
        {
            title ??= "Info";
            await ShowToastAsync(message, title, "alert-primary", "filled", duration);
        }

        private async Task ShowToastAsync(string content, string title, string alertType, string fillType, int? timeShown)
        {
            await _jsRuntime.InvokeVoidAsync("blazorToast.show", content, title, alertType, fillType, timeShown);
        }
    }
}
