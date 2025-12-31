using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using Stats.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class AdvancedStats
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    [Parameter] public int ClientId { get; set; }
    [SupplyParameterFromQuery] public string? serverId { get; set; }

    [PersistentState] public AdvancedStatsInfo? Stats { get; set; }
    private SideContextMenuItems? MenuItems;
    private bool _chartsInitialized;
    private bool _showAllHitLocations;
    private bool _showAllWeapons;
    private const int DefaultTableRowCount = 10;

    protected override async Task OnParametersSetAsync()
    {
        _chartsInitialized = false;
        try
        {
            Stats = await DataService.GetClientStatisticsAsync(ClientId, serverId);
            GenerateMenu();
        }
        catch (Exception)
        {
            NavManager.NavigateTo("/client" + ClientId);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Stats is { TopHitLocations.Count: > 0 } && !_chartsInitialized)
        {
            _chartsInitialized = true;
            try
            {
                // Prepare data for JS charts
                var hitLocationData = Stats.TopHitLocations.Select(loc => new
                {
                    name = loc.InternalName,
                    percentage = loc.Percentage
                }).ToList();

                var maxPercentage = Stats.TopHitLocations.Count != 0
                    ? Stats.TopHitLocations.Max(h => h.Percentage)
                    : 0f;

                await JS.InvokeVoidAsync("initAdvancedStats", Stats.PerformanceHistory, hitLocationData, maxPercentage,
                    AppState.Loc("PLUGINS_STATS_COMMANDS_PERFORMANCE"));
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error calling initAdvancedStats: {ex.Message}");
            }
        }
    }

    private void GenerateMenu()
    {
        MenuItems = new SideContextMenuItems
        {
            MenuTitle = AppState.Loc("WEBFRONT_CONTEXT_MENU_GLOBAL_GAME"),
            Items = Stats?.Servers.Select(server => new SideContextMenuItem
            {
                IsLink = true,
                Reference = $"/client/{ClientId}/stats?serverId={server.Endpoint}",
                Title = server.Name.StripColors(),
                IsActive = Stats.ServerEndpoint == server.Endpoint,
                Meta = server.Game.ToString(),
                IsCollapse = true
            }).Prepend(new SideContextMenuItem
            {
                IsLink = true,
                Reference = $"/client/{ClientId}/stats",
                Title = AppState.Loc("WEBFRONT_STATS_INDEX_ALL_SERVERS"),
                IsActive = Stats.ServerEndpoint == null
            }).ToList()
        };
    }

    /// <summary>
    /// Gets the OpenGraph description for the stats page.
    /// Null-safe for StreamRendering - returns fallback during loading.
    /// </summary>
    private string GetOpenGraphDescription()
    {
        if (Stats is null)
            return "Player statistics and performance data";

        var kd = Stats.Deaths > 0
            ? (Stats.Kills / (double)Stats.Deaths).ToString("0.00")
            : "-";
        
        var perfLabel = Stats.ServerId != null ? "Performance" : "Rating";
        var perfValue = Stats.ServerId != null 
            ? (Stats.Performance?.ToString("0") ?? "No Performance") 
            : (Stats.Rating?.ToString("0") ?? "Unrated");

        return $"{perfLabel}: {perfValue} • {kd} K/D\n{Stats.Kills:N0} kills • {Stats.Deaths:N0} deaths";
    }

    /// <summary>
    /// Gets the OpenGraph image - rank icon or default icon.
    /// </summary>
    private string GetOpenGraphImage()
    {
        return Stats?.ZScore is not null
            ? $"{NavManager.BaseUri}images/stats/ranks/rank_{GetRankIconIndex(Stats.ZScore)}.png"
            : $"{NavManager.BaseUri}images/icon.png";
    }

    private static int GetRankIconIndex(double? zScore)
    {
        if (zScore == null)
        {
            return 0;
        }

        const int zScoreRange = 3;
        const int rankIconDivisions = 24;
        const double divisionIncrement = (zScoreRange * 2) / (double)rankIconDivisions;
        var rank = 1;

        for (var i = rank; i <= rankIconDivisions; i++)
        {
            var bottom = Math.Round(-zScoreRange + (i - 1) * divisionIncrement, 5);
            var top = Math.Round(-zScoreRange + i * divisionIncrement, 5);

            if (zScore > bottom && zScore <= top)
            {
                return rank;
            }

            if (i == 1 && zScore < bottom
                || i == rankIconDivisions && zScore > top)
            {
                return i;
            }

            rank++;
        }

        return 0;
    }
}
