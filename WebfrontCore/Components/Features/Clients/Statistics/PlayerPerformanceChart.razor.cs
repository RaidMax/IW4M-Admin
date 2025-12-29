using IW4MAdmin.Plugins.Stats.Web.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class PlayerPerformanceChart
{
    [Parameter, EditorRequired] public int ClientId { get; set; }
    [Parameter, EditorRequired] public int Id { get; set; }
    [Parameter, EditorRequired] public IEnumerable<PerformanceHistory> History { get; set; } = [];
    private string CanvasId => $"rating_history_{ClientId}_{Id}";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && History.Any())
        {
            try
            {
                var historyData = History.OrderBy(perf => perf.OccurredAt).ToList();
                var rankingText = AppState.Loc("WEBFRONT_ADV_STATS_RANKING_METRIC");

                await JsRuntime.InvokeVoidAsync("getStatsChart", CanvasId, rankingText, historyData);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to init chart {CanvasId}: {ex.Message}");
            }
        }
    }
}
