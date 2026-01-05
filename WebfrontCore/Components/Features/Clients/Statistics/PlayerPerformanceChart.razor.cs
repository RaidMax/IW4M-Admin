using IW4MAdmin.Plugins.Stats.Web.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class PlayerPerformanceChart
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
            await InitializeChartAsync();
        }
        else if (_needsReinitialization)
        {
            _needsReinitialization = false;
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
}
