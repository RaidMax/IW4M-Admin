using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Components.Features.Servers.Components;

/// <summary>
/// Modal wrapper for the zombie live-match snapshot. Mirrors <see cref="ScoreboardModalWrapper"/>
/// — same 5s PeriodicTimer cadence, same dispose pattern. The snapshot service may be null
/// (premium plugin not loaded) — we surface a friendly empty state in that case rather than
/// failing.
/// </summary>
public partial class ZombieLiveModalWrapper : IAsyncDisposable
{
    [Parameter, EditorRequired] public string ServerId { get; set; } = default!;
    [Inject] public IZombieLiveMatchService? LiveMatchService { get; set; }
    [Inject] public required ILogger<ZombieLiveModalWrapper> Logger { get; set; }

    private bool _isLoading = true;
    private string? _error;
    private ZombieLiveMatchSnapshot? _snapshot;
    private PeriodicTimer? _refreshTimer;
    private CancellationTokenSource? _cts;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await LoadDataAsync();

        _cts = new CancellationTokenSource();
        _refreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        _ = RefreshLoopAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            if (LiveMatchService is not null)
            {
                _snapshot = await LiveMatchService.GetLiveMatchSnapshotAsync(ServerId);
            }
            _error = null;
        }
        catch (Exception ex)
        {
            _error = "Could not load live match data";
            Logger.LogError(ex, "Error loading live match snapshot for server {ServerId}", ServerId);
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
                    if (LiveMatchService is not null)
                    {
                        _snapshot = await LiveMatchService.GetLiveMatchSnapshotAsync(ServerId);
                        await InvokeAsync(StateHasChanged);
                    }
                }
                catch
                {
                    // Ignore refresh errors — keep showing the last good snapshot
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
        await (_cts?.CancelAsync() ?? Task.CompletedTask);
        _cts?.Dispose();
        _refreshTimer?.Dispose();
    }
}
