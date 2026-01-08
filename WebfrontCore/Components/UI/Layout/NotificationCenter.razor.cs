using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Alerts;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.UI.Layout;

public partial class NotificationCenter : IDisposable
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IToastService ToastService { get; set; }
    private List<Alert.AlertState> Alerts = [];
    private bool _isOpen = false;

    private void ToggleDropdown()
    {
        _isOpen = !_isOpen;
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            Alerts = (await DataService.GetAlertsAsync()).ToList();
        }
        catch
        {
        }
    }

    private async Task Dismiss(Guid id)
    {
        try
        {
            await DataService.DismissAlertAsync(id);
            Alerts.RemoveAll(a => a.AlertId == id);
            await ToastService.ShowSuccessAsync("Alert dismissed");
        }
        catch (Exception ex)
        {
            await ToastService.ShowErrorAsync($"Failed to dismiss alert: {ex.Message}");
        }
    }

    private async Task DismissAll()
    {
        try
        {
            await DataService.DismissAllAlertsAsync();
            var count = Alerts.Count;
            Alerts.Clear();
            await ToastService.ShowSuccessAsync($"Dismissed {count} alert{(count != 1 ? "s" : string.Empty)}");
        }
        catch (Exception ex)
        {
            await ToastService.ShowErrorAsync($"Failed to dismiss alerts: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // Dispose logic if needed, e.g. unsubscribe events or timers
        // _updateTimer?.Dispose();
    }
}
