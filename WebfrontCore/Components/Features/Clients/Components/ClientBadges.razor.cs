using Microsoft.AspNetCore.Components;
using WebfrontCore.Core.Services;


namespace WebfrontCore.Components.Features.Clients.Components;

public partial class ClientBadges : IDisposable
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IWebfrontApiClient Api { get; set; }

    private NavigationData? _navData;
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
                _navData = await Api.GetNavigationDataAsync();
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
            while (await _badgeRefreshTimer.WaitForNextTickAsync(_cts.Token))
            {
                try
                {
                    _navData = await Api.GetNavigationDataAsync();
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
}
