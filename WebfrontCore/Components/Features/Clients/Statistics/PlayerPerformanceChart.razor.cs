using IW4MAdmin.Plugins.Stats.Web.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class PlayerPerformanceChart : IAsyncDisposable
{
    [Parameter, EditorRequired] public int ClientId { get; set; }
    [Parameter, EditorRequired] public int Id { get; set; }
    [Parameter, EditorRequired] public IEnumerable<PerformanceHistory> History { get; set; } = [];
    [Inject] public required ILogger<PlayerPerformanceChart> Logger { get; set; }
    private string CanvasId => $"rating_history_{ClientId}_{Id}";

    private bool _hasRendered;
    private int _previousClientId;
    private int _previousId;
    private bool _needsReinitialization;
    private string _currentCanvasId;

    protected override void OnParametersSet()
    {
        // Detect if parameters changed after first render - flag for reinitialization
        if (_hasRendered && (_previousClientId != ClientId || _previousId != Id))
        {
            _needsReinitialization = true;
        }
        
        _previousClientId = ClientId;
        _previousId = Id;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _hasRendered = true;
            _currentCanvasId = CanvasId;
            await InitializeChartAsync();
        }
        else if (_needsReinitialization)
        {
            _needsReinitialization = false;
            
            // Destroy the old chart before creating a new one
            if (!string.IsNullOrEmpty(_currentCanvasId))
            {
                try
                {
                    await JsRuntime.InvokeVoidAsync("destroyStatsChart", _currentCanvasId);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex, "Failed to destroy old chart {CanvasId}", _currentCanvasId);
                }
            }
            
            _currentCanvasId = CanvasId;
            await InitializeChartAsync();
        }
    }

    private async Task InitializeChartAsync()
    {
        if (!History.Any())
        {
            return;
        }

        try
        {
            var historyData = History.OrderBy(perf => perf.OccurredAt).ToList();
            var rankingText = AppState.Loc("WEBFRONT_ADV_STATS_RANKING_METRIC");

            await JsRuntime.InvokeVoidAsync("getStatsChart", CanvasId, rankingText, historyData);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to initialize chart {CanvasId}", CanvasId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!string.IsNullOrEmpty(_currentCanvasId))
        {
            try
            {
                await JsRuntime.InvokeVoidAsync("destroyStatsChart", _currentCanvasId);
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected, chart will be cleaned up automatically
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to destroy chart {CanvasId} on dispose", _currentCanvasId);
            }
        }
    }
}
