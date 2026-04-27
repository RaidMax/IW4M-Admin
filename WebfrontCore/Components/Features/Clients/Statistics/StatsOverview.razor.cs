using IW4MAdmin.Plugins.Stats.Web.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class StatsOverview : IAsyncDisposable
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required ILogger<StatsOverview> Logger { get; set; }
    [Inject] public required IHttpContextAccessor HttpContextAccessor { get; set; }

    [SupplyParameterFromQuery(Name = "serverId")]
    public string? ServerId { get; set; }

    [SupplyParameterFromQuery(Name = "category")]
    public string? PerformanceBucket { get; set; }

    [PersistentState(AllowUpdates = true)] public StatsOverviewState? State { get; set; }

    private bool _hasLoaded;
    private string? _previousServerId;
    private string? _previousBucket;
    private List<BucketInfo>? _availableBuckets;
    private bool _showBucketSelector;
    private bool _firstLoad = true;
    private bool _isLoadingMore;

    private const int InitialBatchSize = 25;
    private const int LoadMoreBatchSize = 25;

    protected override async Task OnParametersSetAsync()
    {
        // Initialize state if not restored
        if (State is null)
        {
            State = new StatsOverviewState();
        }
        // Check if we have restored state that matches the current request
        else if (_firstLoad && State.ServerId == ServerId && State.PerformanceBucket == PerformanceBucket && State.TopPlayers.Count > 0)
        {
            _firstLoad = false;
            _previousServerId = ServerId;
            _previousBucket = PerformanceBucket;
            _hasLoaded = true;

            if (State.MenuItems is null)
            {
                await GenerateMenu();
            }

            return;
        }

        // Refresh grid when ServerId or bucket changes
        if (_firstLoad || _previousServerId != ServerId || _previousBucket != PerformanceBucket)
        {
            _firstLoad = false;
            _previousServerId = ServerId;
            _previousBucket = PerformanceBucket;
            _hasLoaded = false;
            _showBucketSelector = false;

            // Clear loaded items on filter change.
            State.TopPlayers.Clear();
            State.HasMore = true;
            State.ServerId = ServerId;
            State.PerformanceBucket = PerformanceBucket;

            var allServers = await DataService.GetServersAsync();
            var buckets = allServers
                .Where(s => !string.IsNullOrEmpty(s.PerformanceBucket))
                .GroupBy(s => s.PerformanceBucket, StringComparer.OrdinalIgnoreCase)
                .Select(g => new BucketInfo
                {
                    Code = g.Key,
                    Games = g.Select(s => s.Game.ToString()).Distinct().ToList(),
                    ServerCount = g.Count()
                })
                .OrderBy(b => b.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Validate category if provided (case-insensitive)
            if (PerformanceBucket != null)
            {
                var matchedBucket = buckets.FirstOrDefault(b =>
                    string.Equals(b.Code, PerformanceBucket, StringComparison.OrdinalIgnoreCase));
                if (matchedBucket == null)
                {
                    if (HttpContextAccessor.HttpContext is { } httpContext)
                    {
                        httpContext.Response.StatusCode = 404;
                    }

                    NavManager.NavigateTo("/NotFound", replace: true);
                    return;
                }

                // Normalize to the canonical case
                PerformanceBucket = matchedBucket.Code;
            }

            // When no bucket or server is selected, auto-select the first bucket alphabetically
            if (ServerId == null && PerformanceBucket == null)
            {
                if (buckets.Count > 0)
                {
                    PerformanceBucket = buckets[0].Code;
                    State.PerformanceBucket = PerformanceBucket;
                }

                State.SelectedServer = null;
            }
            else if (ServerId != null)
            {
                var servers = await DataService.GetServersAsync();
                State.SelectedServer = servers.FirstOrDefault(s => s.Endpoint == ServerId);
            }
            else
            {
                State.SelectedServer = null;
            }

            await GenerateMenu();
            await LoadBatch(InitialBatchSize);
            _hasLoaded = true;
        }
    }

    private async Task LoadBatch(int count)
    {
        if (State is null) return;

        try
        {
            var response = await DataService.GetTopStatsAsync(new WebfrontCore.Controllers.API.Models.TopStatsRequest
            {
                Count = count,
                Offset = State.TopPlayers.Count,
                ServerId = ServerId,
                PerformanceBucketCode = PerformanceBucket
            });

            State.TotalRankedClients = response.TotalRankedClients;
            State.TopPlayers.AddRange(response.Players);

            // No more pages when the server returned fewer than asked, or we've
            // reached the total count it advertised.
            State.HasMore = response.Players.Count >= count
                            && State.TopPlayers.Count < (int)response.TotalRankedClients;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading stats batch");
            State.HasMore = false;
        }
    }

    private async Task LoadMore()
    {
        if (_isLoadingMore || State is null || !State.HasMore) return;
        _isLoadingMore = true;
        StateHasChanged();
        try
        {
            await LoadBatch(LoadMoreBatchSize);
        }
        finally
        {
            _isLoadingMore = false;
            StateHasChanged();
        }
    }

    private async Task GenerateMenu()
    {
        if (State == null) return;
        var servers = await DataService.GetServersAsync();

        var items = new List<SideContextMenuItem>();

        // "Category" section header + bucket items (alphabetical)
        var bucketGroups = servers
            .Where(s => !string.IsNullOrEmpty(s.PerformanceBucket))
            .GroupBy(s => s.PerformanceBucket, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (bucketGroups.Count > 0)
        {
            items.Add(new SideContextMenuItem { IsSectionHeader = true, Title = "Category" });
            items.AddRange(bucketGroups.Select(group => new SideContextMenuItem
            {
                IsLink = true,
                Reference = $"/stats/top?category={group.Key}",
                Title = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(group.Key.ToLower()),
                IsActive = string.Equals(PerformanceBucket, group.Key, StringComparison.OrdinalIgnoreCase) && ServerId == null,
                Meta = group.First().Game.ToString(),
                IsCollapse = false
            }));
        }

        // Individual servers filtered by selected bucket (collapsible, grouped by game)
        var filteredServers = PerformanceBucket != null
            ? servers.Where(s => string.Equals(s.PerformanceBucket, PerformanceBucket, StringComparison.OrdinalIgnoreCase))
            : servers;

        items.AddRange(filteredServers.Select(server => new SideContextMenuItem
        {
            IsLink = true,
            Reference = $"/stats/top?serverId={server.Endpoint}",
            Title = server.Name.StripColors(),
            IsActive = ServerId == server.Endpoint,
            Meta = server.Game.ToString(),
            IsCollapse = true
        }));

        State.MenuItems = new SideContextMenuItems
        {
            MenuTitle = AppState.Loc("WEBFRONT_CONTEXT_MENU_GLOBAL_GAME"),
            Items = items
        };
    }

    // Properties that proxy to State
    public long TotalRankedClients => State?.TotalRankedClients ?? 0;
    public ServerInfo? SelectedServer => State?.SelectedServer;
    public List<TopStatsInfo>? TopPlayers => State?.TopPlayers;

    public SideContextMenuItems MenuItems
    {
        get => State?.MenuItems ?? new SideContextMenuItems();
        set
        {
            if (State != null) State.MenuItems = value;
        }
    }

    public class StatsOverviewState
    {
        public long TotalRankedClients { get; set; }
        public ServerInfo? SelectedServer { get; set; }
        public List<TopStatsInfo> TopPlayers { get; set; } = [];
        public string? ServerId { get; set; }
        public string? PerformanceBucket { get; set; }
        public SideContextMenuItems? MenuItems { get; set; }
        public bool HasMore { get; set; } = true;
    }

    // Existing helper methods...
    private static int GetRankIconIndex(double? zScore)
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

    /// <summary>
    /// Gets the OpenGraph description with top 3 players.
    /// </summary>
    private string GetOpenGraphDescription()
    {
        var serverName = State?.SelectedServer?.Name.StripColors() ?? AppState.Loc("WEBFRONT_STATS_INDEX_ALL_SERVERS");

        if (State?.TopPlayers == null || State.TopPlayers.Count == 0)
            return $"{serverName} — {State?.TotalRankedClients ?? 0:N0} ranked players";

        var topList = State.TopPlayers.Take(3).Select((p, idx) =>
            $"#{idx + 1} {p.Name.StripColors()} ({p.Performance:0} / {p.KDR:0.00})"
        );

        return $"{serverName} — {State.TotalRankedClients:N0} ranked\n{string.Join("\n", topList)}";
    }

    /// <summary>
    /// Gets the OpenGraph image - top player's rank icon or default.
    /// </summary>
    private string GetOpenGraphImage()
    {
        var topPlayer = State?.TopPlayers.FirstOrDefault();
        return topPlayer?.ZScore != null
            ? $"{NavManager.BaseUri}images/stats/ranks/rank_{GetRankIconIndex(topPlayer.ZScore)}.png"
            : $"{NavManager.BaseUri}images/icon.png";
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
    }

    public class BucketInfo
    {
        public string Code { get; set; }
        public List<string> Games { get; set; } = [];
        public int ServerCount { get; set; }
    }
}
