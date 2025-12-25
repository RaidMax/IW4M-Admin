using IW4MAdmin.Plugins.Stats.Web.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class StatsOverview : IAsyncDisposable
{
    [Inject] public required IJSRuntime Runtime { get; set; }
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Parameter]
    public string serverId { get; set; }

    // public for tests
    public List<TopStatsInfo> TopPlayers { get; set; }
    public SideContextMenuItems MenuItems { get; set; }

    private long _totalRankedClients;

    public long TotalRankedClients
    {
        get => _totalRankedClients;
        set => _totalRankedClients = value;
    }

    public ServerInfo SelectedServer { get; set; }
 
    private bool IsLoading = false;
    private bool HasMore = true;
    private bool _hasLoaded = false;
    private string _previousServerId;
    private bool _firstLoad = true;
    private bool _localizationInitialized;
    private Virtualize<TopStatsInfo> _virtualizeComponent;

    protected override async Task OnParametersSetAsync()
    {
        // Refresh grid when serverId changes
        if (_firstLoad || _previousServerId != serverId)
        {
            _firstLoad = false;
            _previousServerId = serverId;
            TopPlayers = null;
            _hasLoaded = false;
            
            if (_virtualizeComponent != null)
            {
                await _virtualizeComponent.RefreshDataAsync();
            }

            await GenerateMenu();
            
             if (serverId != null)
            {
                var servers = await DataService.GetServersAsync();
                SelectedServer = servers.FirstOrDefault(s => s.Endpoint == serverId);
            }
            else
            {
                SelectedServer = null;
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_localizationInitialized)
        {
            var localization = new Dictionary<string, string>
            {
                { "WEBFRONT_ADV_STATS_RANKING_METRIC", AppState.Loc("WEBFRONT_ADV_STATS_RANKING_METRIC") },
                { "PLUGINS_STATS_COMMANDS_PERFORMANCE", AppState.Loc("PLUGINS_STATS_COMMANDS_PERFORMANCE") }
            };
            try 
            {
                await Runtime.InvokeVoidAsync("eval", $"window._localization = {System.Text.Json.JsonSerializer.Serialize(localization)};");
                _localizationInitialized = true;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error initializing localization: {ex.Message}");
            }
        }
    }

    private async ValueTask<ItemsProviderResult<TopStatsInfo>> LoadPlayerStats(ItemsProviderRequest request)
    {
        // Calculate offset and count from the request
        var count = Math.Min(request.Count, 50); // Limit chunk size
        var offset = request.StartIndex;

        try
        {
            var response = await DataService.GetTopStatsAsync(new WebfrontCore.Controllers.API.Models.TopStatsRequest
            {
                Count = count,
                Offset = offset,
                ServerId = serverId
            });

            // Update total count if it changed, but don't force re-render just for this significantly
            if (_totalRankedClients != response.TotalRankedClients)
            {
                TotalRankedClients = response.TotalRankedClients;
                _hasLoaded = true;
                StateHasChanged(); 
            }
            else if (!_hasLoaded)
            {
                _hasLoaded = true;
                StateHasChanged();
            }

            return new ItemsProviderResult<TopStatsInfo>(response.Players, (int)response.TotalRankedClients);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error loading stats: {ex.Message}");
            return new ItemsProviderResult<TopStatsInfo>(new List<TopStatsInfo>(), 0);
        }
    }

    private async Task GenerateMenu()
    {
        var servers = await DataService.GetServersAsync();

        MenuItems = new SideContextMenuItems
        {
            MenuTitle = AppState.Loc("WEBFRONT_CONTEXT_MENU_GLOBAL_GAME"),
            Items = servers.Select(server => new SideContextMenuItem
            {
                IsLink = true,
                Reference = $"/stats/top?serverId={server.Endpoint}",
                Title = server.Name.StripColors(),
                IsActive = serverId == server.Endpoint,
                Meta = server.Game.ToString(),
                IsCollapse = true
            }).Prepend(new SideContextMenuItem
            {
                IsLink = true,
                Reference = "/stats/top",
                Title = AppState.Loc("WEBFRONT_STATS_INDEX_ALL_SERVERS"),
                IsActive = serverId == null
            }).ToList()
        };
    }

    private int GetRankIconIndex(double? zScore)
    {
        // Logic from IW4MAdmin.Plugins.Stats.Extensions.RankIconIndexForZScore
        if (zScore == null)
        {
            return 0;
        }

        const int ZScoreRange = 3;
        const int RankIconDivisions = 24;
        const double divisionIncrement = (ZScoreRange * 2) / (double)RankIconDivisions;
        var rank = 1;

        for (var i = rank; i <= RankIconDivisions; i++)
        {
            var bottom = Math.Round(-ZScoreRange + (i - 1) * divisionIncrement, 5);
            var top = Math.Round(-ZScoreRange + i * divisionIncrement, 5);

            if (zScore > bottom && zScore <= top)
            {
                return rank;
            }

            if (i == 1 && zScore < bottom // catch all for really bad players
                // catch all for very good players
                || i == RankIconDivisions && zScore > top)
            {
                return i;
            }

            rank++;
        }

        return 0;
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
    }
}
