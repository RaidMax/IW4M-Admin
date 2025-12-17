using IW4MAdmin.Plugins.Stats.Web.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using WebfrontCore.Components.UI.Navigation;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class StatsOverview
{
    [Inject] public required IJSRuntime Runtime { get; set; }
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IZeroJsInterop JsInterop { get; set; }
    [SupplyParameterFromQuery] public string serverId { get; set; }

    private List<TopStatsInfo> TopPlayers;
    private long TotalRankedClients;
    private ServerInfo SelectedServer;
    private SideContextMenuItems MenuItems;
    private int Offset = 0;
    private const int Count = 25;
    private bool IsLoading = false;
    private bool HasMore = true;
    private ElementReference _loadMoreTrigger;
    private DotNetObjectReference<StatsOverview> _objRef;
    private bool _observerSetup;
    private string _previousServerId;
    private bool _chartsInitialized;
    private bool _firstLoad = true;
    private bool _localizationInitialized;

    protected override async Task OnParametersSetAsync()
    {
        // Load on first load OR when serverId changes
        if (_firstLoad || _previousServerId != serverId)
        {
            _firstLoad = false;
            _previousServerId = serverId;
            Offset = 0;
            TopPlayers = null;
            HasMore = true;
            _observerSetup = false;
            _chartsInitialized = false;

            await LoadData();
            await GenerateMenu();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Setup infinite scroll
        if (TopPlayers != null && TopPlayers.Any() && HasMore && !_observerSetup)
        {
            _objRef = DotNetObjectReference.Create(this);
            await JsInterop.SetupInfiniteScroll(_loadMoreTrigger, _objRef);
            _observerSetup = true;
        }

        // Initialize performance charts for each player
        if (TopPlayers != null && TopPlayers.Any() && !_chartsInitialized)
        {
            await Task.Delay(100); // Delay to ensure DOM is ready
            _chartsInitialized = true;
            
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

            try
            {
                await JsInterop.InitTopPlayersCharts();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error initializing charts: {ex.Message}");
            }
        }
    }

    private async Task LoadData()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            var response = await Api.GetTopPlayersAsync(Count, Offset, serverId);
            var newPlayers = response.Players;
            TotalRankedClients = response.TotalRankedClients;

            if (newPlayers.Count < Count)
            {
                HasMore = false;
            }

            if (TopPlayers == null)
            {
                TopPlayers = newPlayers;
            }
            else
            {
                TopPlayers.AddRange(newPlayers);
            }

            if (serverId != null)
            {
                var servers = await Api.GetServersAsync();
                SelectedServer = servers.FirstOrDefault(s => s.Endpoint == serverId);
            }
            else
            {
                SelectedServer = null;
            }
        }
        catch (Exception)
        {
            HasMore = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [JSInvokable]
    public async Task LoadMore()
    {
        if (IsLoading || !HasMore) return;

        Offset += Count;
        await LoadData();
        StateHasChanged();

        // Initialize charts for newly loaded items after DOM updates
        await Task.Delay(150); // Give DOM time to render new elements
        try
        {
            await JsInterop.InitTopPlayersCharts();
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error initializing charts after load more: {ex.Message}");
        }
    }

    private async Task GenerateMenu()
    {
        var servers = await Api.GetServersAsync();

        MenuItems = new SideContextMenuItems
        {
            MenuTitle = AppState.Loc("WEBFRONT_CONTEXT_MENU_GLOBAL_GAME"),
            Items = servers.Select(server => new SideContextMenuItem
            {
                IsLink = true,
                Reference = $"/Client/Statistics?serverId={server.Endpoint}",
                Title = server.Name.StripColors(),
                IsActive = serverId == server.Endpoint,
                Meta = server.Game.ToString(),
                IsCollapse = true
            }).Prepend(new SideContextMenuItem
            {
                IsLink = true,
                Reference = "/Client/Statistics",
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
        if (_observerSetup)
        {
            try
            {
                await JsInterop.RemoveInfiniteScroll(_loadMoreTrigger);
            }
            catch
            {
            }
        }

        _objRef?.Dispose();
    }
}
