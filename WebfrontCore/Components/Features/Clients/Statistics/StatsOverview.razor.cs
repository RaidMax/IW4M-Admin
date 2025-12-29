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

    [SupplyParameterFromQuery(Name = "serverId")]
    public string? ServerId { get; set; }

    public required SideContextMenuItems MenuItems { get; set; }
    private long TotalRankedClients { get; set; }
    private ServerInfo? SelectedServer { get; set; }

    private bool _hasLoaded;
    private string? _previousServerId;
    private bool _firstLoad = true;
    private Virtualize<TopStatsInfo>? _virtualizeComponent;
    private Dictionary<int, List<TopStatsInfo>> _statsCache = new();

    protected override async Task OnParametersSetAsync()
    {
        // Refresh grid when ServerId changes
        if (_firstLoad || _previousServerId != ServerId)
        {
            _firstLoad = false;
            _previousServerId = ServerId;
            _hasLoaded = false;
            
            // Clear cache when server changes
            _statsCache.Clear();

            if (_virtualizeComponent != null)
            {
                await _virtualizeComponent.RefreshDataAsync();
            }

            await GenerateMenu();

            if (ServerId != null)
            {
                var servers = await DataService.GetServersAsync();
                SelectedServer = servers.FirstOrDefault(s => s.Endpoint == ServerId);
            }
            else
            {
                SelectedServer = null;
            }
        }
    }

    private async ValueTask<ItemsProviderResult<TopStatsInfo>> LoadPlayerStats(ItemsProviderRequest request)
    {
        // Calculate offset and count from the request
        var count = request.Count;
        var offset = request.StartIndex;

        // Check cache first to avoid expensive backend calculations
        if (_statsCache.TryGetValue(offset, out var cachedData) && TotalRankedClients > 0)
        {
            return new ItemsProviderResult<TopStatsInfo>(cachedData, (int)TotalRankedClients);
        }

        try
        {
            var response = await DataService.GetTopStatsAsync(new WebfrontCore.Controllers.API.Models.TopStatsRequest
            {
                Count = count,
                Offset = offset,
                ServerId = ServerId
            });

            // Update total count if it changed, but don't force re-render just for this significantly
            if (TotalRankedClients != response.TotalRankedClients)
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

            // Cache the results for future scrolling
            var playersList = response.Players.ToList();
            _statsCache[offset] = playersList;

            return new ItemsProviderResult<TopStatsInfo>(playersList, (int)response.TotalRankedClients);
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
                IsActive = ServerId == server.Endpoint,
                Meta = server.Game.ToString(),
                IsCollapse = true
            }).Prepend(new SideContextMenuItem
            {
                IsLink = true,
                Reference = "/stats/top",
                Title = AppState.Loc("WEBFRONT_STATS_INDEX_ALL_SERVERS"),
                IsActive = ServerId == null
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

            if (i == 1 && zScore < bottom // catch all for really bad players
                // catch all for very good players
                || i == rankIconDivisions && zScore > top)
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
