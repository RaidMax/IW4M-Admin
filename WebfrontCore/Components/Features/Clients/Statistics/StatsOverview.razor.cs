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
    private readonly Dictionary<int, TopStatsInfo> _statsCache = new();

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

    private const int BatchSize = 50;

    private async ValueTask<ItemsProviderResult<TopStatsInfo>> LoadPlayerStats(ItemsProviderRequest request)
    {
        var startIndex = request.StartIndex;
        var requestedCount = request.Count;

        // Try to fulfill entirely from cache first
        if (TotalRankedClients > 0)
        {
            var cachedItems = new List<TopStatsInfo>();
            var allCached = true;

            var actualEnd = Math.Min(startIndex + requestedCount, (int)TotalRankedClients);
            for (var i = startIndex; i < actualEnd; i++)
            {
                if (_statsCache.TryGetValue(i, out var item))
                {
                    cachedItems.Add(item);
                }
                else
                {
                    allCached = false;
                    break;
                }
            }

            if (allCached && cachedItems.Count > 0)
            {
                return new ItemsProviderResult<TopStatsInfo>(cachedItems, (int)TotalRankedClients);
            }
        }

        try
        {
            // Fetch a batch starting from a position that covers the request
            var fetchCount = Math.Max(BatchSize, requestedCount);

            var response = await DataService.GetTopStatsAsync(new WebfrontCore.Controllers.API.Models.TopStatsRequest
            {
                Count = fetchCount,
                Offset = startIndex,
                ServerId = ServerId
            });

            // Update total count
            TotalRankedClients = response.TotalRankedClients;
            if (!_hasLoaded)
            {
                _hasLoaded = true;
                StateHasChanged();
            }

            // Cache all fetched items
            var playersList = response.Players.ToList();
            for (var i = 0; i < playersList.Count; i++)
            {
                _statsCache[startIndex + i] = playersList[i];
            }

            // Return items for the requested range
            var result = new List<TopStatsInfo>();
            var resultEnd = Math.Min(startIndex + requestedCount, (int)response.TotalRankedClients);
            for (var i = startIndex; i < resultEnd; i++)
            {
                if (_statsCache.TryGetValue(i, out var item))
                {
                    result.Add(item);
                }
            }

            return new ItemsProviderResult<TopStatsInfo>(result, (int)response.TotalRankedClients);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[Virtualize] Error: {ex.Message}");
            return new ItemsProviderResult<TopStatsInfo>(new List<TopStatsInfo>(), (int)TotalRankedClients);
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
