using Microsoft.AspNetCore.Components;
using WebfrontCore.Components.Features.Admin.Components;
using WebfrontCore.Components.UI.Navigation.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.UI.Layout;

/// <summary>
/// Live "players online / admins / reports / flagged" counters shown in the navigation rail.
/// Polls navigation data every few seconds while mounted.
/// </summary>
public partial class NavCounters
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required IActionService ActionService { get; set; }

    [Parameter] public bool Collapsed { get; set; }
    [Parameter] public EventCallback OnNavigate { get; set; }

    private NavigationInfo? _navData;
    private PeriodicTimer? _refreshTimer;
    private CancellationTokenSource? _cts;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _navData = await DataService.GetNavigationDataAsync();
        }
        catch
        {
            // Ignore initial fetch errors
        }
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _refreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            while (_refreshTimer != null && _cts != null && await _refreshTimer.WaitForNextTickAsync(_cts.Token))
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
        _refreshTimer?.Dispose();
    }

    private void ShowReports()
    {
        ActionService.OpenCustom(builder =>
        {
            builder.OpenComponent<DashboardReports>(0);
            builder.CloseComponent();
        }, AppState.Loc("WEBFRONT_MODAL_REPORTS_TITLE"));
        _ = OnNavigate.InvokeAsync();
    }

    private void ShowAdmins()
    {
        ActionService.OpenCustom(builder =>
        {
            builder.OpenComponent<DashboardAdmins>(0);
            builder.CloseComponent();
        }, AppState.Loc("WEBFRONT_MODAL_ADMINS_TITLE"));
        _ = OnNavigate.InvokeAsync();
    }

    private void ShowFlagged()
    {
        ActionService.OpenCustom(builder =>
        {
            builder.OpenComponent<DashboardFlagged>(0);
            builder.CloseComponent();
        }, AppState.Loc("WEBFRONT_MODAL_FLAGGED_TITLE"));
        _ = OnNavigate.InvokeAsync();
    }
}
