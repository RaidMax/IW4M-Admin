using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using Stats.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class AdvancedStats
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }
    [Inject] public required ILogger<AdvancedStats> Logger { get; set; }
    [Inject] public required IServiceProvider ServiceProvider { get; set; }

    [Parameter] public int ClientId { get; set; }
    [SupplyParameterFromQuery] public string? serverId { get; set; }
    [SupplyParameterFromQuery(Name = "category")] public string? performanceBucket { get; set; }

    [PersistentState(AllowUpdates = true)] public AdvancedStatsState? State { get; set; }

    // Convenience accessor
    private AdvancedStatsInfo? Stats => State?.Stats;

    private SideContextMenuItems? MenuItems;
    private bool _chartsInitialized;
    private bool _showAllHitLocations;
    private bool _showAllWeapons;
    private List<ZombieMatchHistoryMatch>? _matchHistory;
    private bool _hasMoreMatches = true;
    private bool _loadingMoreMatches;
    private const int MatchHistoryPageSize = 5;
    private const int DefaultTableRowCount = 10;
    private int _lastLoadedId;
    private string? _lastLoadedServerId;
    private string? _lastLoadedBucket;

    protected override async Task OnParametersSetAsync()
    {
        _chartsInitialized = false;

        // Check if state is restored and matches current parameters
        if (State?.Stats != null &&
            _lastLoadedId == ClientId &&
            State.Stats.ClientId == ClientId &&
            EqualityComparer<string?>.Default.Equals(_lastLoadedServerId, serverId) &&
            EqualityComparer<string?>.Default.Equals(_lastLoadedBucket, performanceBucket))
        {
            // Verify server endpoint match if serverId param is provided
            if (serverId == null || State.Stats.ServerEndpoint == serverId)
            {
                GenerateMenu();
                return;
            }
        }

        try
        {
            State ??= new AdvancedStatsState();

            State.Stats = await DataService.GetClientStatisticsAsync(ClientId, serverId, performanceBucket);
            _lastLoadedId = ClientId;
            _lastLoadedServerId = serverId;
            _lastLoadedBucket = performanceBucket;

            // Load match history from premium service if available
            var matchHistoryService = ServiceProvider.GetService<IZombieMatchHistoryService>();
            if (matchHistoryService is not null)
            {
                _matchHistory = await matchHistoryService.GetPlayerMatchHistoryAsync(ClientId, serverId,
                    0, MatchHistoryPageSize);
                _hasMoreMatches = _matchHistory.Count >= MatchHistoryPageSize;
            }

            GenerateMenu();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load advanced stats for client {ClientId}", ClientId);
            NavManager.NavigateTo("/client/" + ClientId);
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
                Logger.LogWarning(ex, "Error calling initAdvancedStats for client {ClientId}", ClientId);
            }
        }
    }

    private async Task LoadMoreMatches()
    {
        if (_loadingMoreMatches || !_hasMoreMatches || _matchHistory is null) return;

        _loadingMoreMatches = true;

        var matchHistoryService = ServiceProvider.GetService<IZombieMatchHistoryService>();
        if (matchHistoryService is not null)
        {
            var moreMatches = await matchHistoryService.GetPlayerMatchHistoryAsync(ClientId, serverId,
                _matchHistory.Count, MatchHistoryPageSize);
            _matchHistory.AddRange(moreMatches);
            _hasMoreMatches = moreMatches.Count >= MatchHistoryPageSize;
        }

        _loadingMoreMatches = false;
    }

    private void GenerateMenu()
    {
        if (Stats == null) return;

        var items = new List<SideContextMenuItem>
        {
            new()
            {
                IsLink = true,
                Reference = $"/client/{ClientId}/stats",
                Title = AppState.Loc("WEBFRONT_STATS_INDEX_ALL_SERVERS"),
                IsActive = serverId == null && performanceBucket == null
            }
        };

        // "Category" section header + bucket items (alphabetical)
        var bucketGroups = Stats.Servers
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
                Reference = $"/client/{ClientId}/stats?category={group.Key}",
                Title = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(group.Key.ToLower()),
                IsActive = string.Equals(performanceBucket, group.Key, StringComparison.OrdinalIgnoreCase) && serverId == null,
                Meta = group.First().Game.ToString(),
                IsCollapse = false
            }));
        }

        // Individual servers filtered by selected bucket (collapsible, grouped by game)
        var filteredServers = performanceBucket != null
            ? Stats.Servers.Where(s => string.Equals(s.PerformanceBucket, performanceBucket, StringComparison.OrdinalIgnoreCase))
            : Stats.Servers;

        items.AddRange(filteredServers.Select(server => new SideContextMenuItem
        {
            IsLink = true,
            Reference = $"/client/{ClientId}/stats?serverId={server.Endpoint}",
            Title = server.Name.StripColors(),
            IsActive = Stats.ServerEndpoint == server.Endpoint && performanceBucket == null,
            Meta = server.Game.ToString(),
            IsCollapse = true
        }));

        MenuItems = new SideContextMenuItems
        {
            MenuTitle = AppState.Loc("WEBFRONT_CONTEXT_MENU_GLOBAL_GAME"),
            Items = items
        };
    }

    /// <summary>
    /// Gets the OpenGraph description for the stats page.
    /// Null-safe for StreamRendering - returns fallback during loading.
    /// </summary>
    private string GetOpenGraphDescription()
    {
        // 1. Early exit for null stats
        if (Stats is null)
            return "Player statistics and performance data";

        // 2. Calculate K/D Ratio
        var kd = Stats.Deaths > 0
            ? (Stats.Kills / (double)Stats.Deaths).ToString("0.00")
            : "-";

        // 3. Determine Primary Metric (Performance vs Rating)
        string primaryStatDisplay;

        if (Stats.ServerId != null)
        {
            // Server Context: Use Performance
            primaryStatDisplay = Stats.Performance.HasValue
                ? $"{Stats.Performance:0} performance"
                : "No Performance";
        }
        else
        {
            // Global Context: Use Rating
            primaryStatDisplay = Stats.Rating.HasValue
                ? $"{Stats.Rating:0} rating"
                : "Unrated";
        }

        // 4. Return formatted string
        return $"{primaryStatDisplay} • {kd} K/D\n{Stats.Kills:N0} kills • {Stats.Deaths:N0} deaths";
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

    public class AdvancedStatsState
    {
        public AdvancedStatsInfo? Stats { get; set; }
    }
}
