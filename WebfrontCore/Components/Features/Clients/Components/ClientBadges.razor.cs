using Microsoft.AspNetCore.Components;
using WebfrontCore.Components.Features.Admin.Components;
using WebfrontCore.Core.Services;
using WebfrontCore.Components.UI.Navigation.Models;


namespace WebfrontCore.Components.Features.Clients.Components;

public partial class ClientBadges : IDisposable
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required IActionService ActionService { get; set; }

    [Parameter]
    public bool IconOnly { get; set; }

    private NavigationInfo? _navData;
    private PeriodicTimer? _badgeRefreshTimer;
    private CancellationTokenSource? _cts;

    protected override void OnInitialized()
    {
        _cts = new CancellationTokenSource();
        _badgeRefreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        _ = RefreshBadgesAsync();
        
        // Initial load
        Task.Run(async () =>
        {
            try
            {
                // todo: we don't need all the from here
                _navData = await DataService.GetNavigationDataAsync();
                await InvokeAsync(StateHasChanged);
            }
            catch
            {
                // Ignored
            }
        });
        
        AppState.OnChange += StateHasChanged;
    }

    private async Task RefreshBadgesAsync()
    {
        try
        {
            while (_badgeRefreshTimer != null && _cts != null && await _badgeRefreshTimer.WaitForNextTickAsync(_cts.Token))
            {
                try
                {
                    _navData = await DataService.GetNavigationDataAsync();
                    await InvokeAsync(StateHasChanged);
                }
                catch
                {
                    // Ignore refresh errors
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when component is disposed
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _badgeRefreshTimer?.Dispose();
        AppState.OnChange -= StateHasChanged;
    }

    private void ShowReports()
    {
        ActionService.OpenCustom(builder =>
        {
            builder.OpenComponent<DashboardReports>(0);
            builder.CloseComponent();
        }, AppState.Loc("WEBFRONT_MODAL_REPORTS_TITLE"));
    }

    private void ShowAdmins()
    {
        ActionService.OpenCustom(builder =>
        {
            builder.OpenComponent<DashboardAdmins>(0);
            builder.CloseComponent();
        }, AppState.Loc("WEBFRONT_MODAL_ADMINS_TITLE"));
    }

    private void ShowFlagged()
    {
        ActionService.OpenCustom(builder =>
        {
            builder.OpenComponent<DashboardFlagged>(0);
            builder.CloseComponent();
        }, AppState.Loc("WEBFRONT_MODAL_FLAGGED_TITLE"));
    }
}
