using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Alerts;
using WebfrontCore.Services;

namespace WebfrontCore.Components.Shared.Partials;

public partial class Notifications
{
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IToastService ToastService { get; set; }
    private List<Alert.AlertState> Alerts = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            Alerts = (await Api.GetAlertsAsync()).ToList();
        }
        catch
        {
        }
    }

    private async Task Dismiss(Guid id)
    {
        try
        {
            await Api.DismissAlertAsync(id);
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
            await Api.DismissAllAlertsAsync();
            var count = Alerts.Count;
            Alerts.Clear();
            await ToastService.ShowSuccessAsync($"Dismissed {count} alert{(count != 1 ? "s" : string.Empty)}");
        }
        catch (Exception ex)
        {
            await ToastService.ShowErrorAsync($"Failed to dismiss alerts: {ex.Message}");
        }
    }
}
