using Microsoft.AspNetCore.Components;
using WebfrontCore.Components.Features.Servers.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Servers.Components;

public partial class ScoreboardModalWrapper : IAsyncDisposable
{
    [Parameter] public string ServerId { get; set; }
    [Inject] public required IWebfrontDataService ServerDataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    
    private bool _isLoading = true;
    private string? _error;
    private ScoreboardInfo? _scoreboardInfo;
    private PeriodicTimer? _refreshTimer;
    private CancellationTokenSource? _cts;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }
        
        await LoadDataAsync();
        
        // Start refresh timer
        _cts = new CancellationTokenSource();
        _refreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        _ = RefreshLoopAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _scoreboardInfo = await ServerDataService.GetServerScoreboardAsync(ServerId);
            _error = null;
        }
        catch (Exception ex)
        {
            _error = AppState.Loc("WEBFRONT_SCOREBOARD_ERROR_LOADING");
            System.Console.WriteLine(ex);
        }
        finally
        {
            _isLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task RefreshLoopAsync()
    {
        if (_refreshTimer == null || _cts == null) return;
        
        try
        {
            while (await _refreshTimer.WaitForNextTickAsync(_cts.Token))
            {
                try
                {
                    _scoreboardInfo = await ServerDataService.GetServerScoreboardAsync(ServerId);
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
            // Expected on dispose
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _refreshTimer?.Dispose();
    }
}
