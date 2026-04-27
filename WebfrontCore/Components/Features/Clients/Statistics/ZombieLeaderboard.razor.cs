using Data.Models;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class ZombieLeaderboard
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required ILogger<ZombieLeaderboard> Logger { get; set; }
    [Inject] public required IHttpContextAccessor HttpContextAccessor { get; set; }
    [Inject] public required IServiceProvider ServiceProvider { get; set; }

    [SupplyParameterFromQuery(Name = "game")]
    public string? GameParam { get; set; }

    [SupplyParameterFromQuery(Name = "map")]
    public string? MapParam { get; set; }

    [SupplyParameterFromQuery(Name = "players")]
    public string? PlayersParam { get; set; }

    private IZombieLeaderboardService? _leaderboardService;
    private IZombieMatchHistoryService? _matchHistoryService;
    private ZombieLeaderboardMetadata? _metadata;
    private ZombieLeaderboardGame? _selectedGame;
    private ZombieLeaderboardMap? _selectedMap;
    private int _selectedPlayerCount;
    private bool _hasLoaded;
    private bool _serviceAvailable;
    private int _totalEntries;
    private bool _entriesLoaded;
    private bool _isLoadingMore;
    private bool _hasMore = true;
    private readonly List<ZombieLeaderboardEntry> _entries = [];
    private SharedLibraryCore.Dtos.SideContextMenuItems? MenuItems { get; set; }
    private readonly Dictionary<int, SharedLibraryCore.Interfaces.ZombieMatchDetail?> _expandedMatches = new();
    private readonly HashSet<int> _loadingMatches = new();
    private List<ZombieMapStatRecord> _mapRecords = [];

    private const int InitialBatchSize = 25;
    private const int LoadMoreBatchSize = 25;

    private string? _previousGame;
    private string? _previousMap;
    private string? _previousPlayers;
    private bool _firstLoad = true;

    protected override async Task OnParametersSetAsync()
    {
        _leaderboardService = ServiceProvider.GetService<IZombieLeaderboardService>(); // TODO: Replace with IResourceQueryHelper<AdvancedClientStatsResourceQueryHelper> (<AdvancedClientStatsResourceQueryHelper> REFERENCE!)
        _matchHistoryService = ServiceProvider.GetService<IZombieMatchHistoryService>(); // TODO: Replace with IResourceQueryHelper<AdvancedClientStatsResourceQueryHelper> (<AdvancedClientStatsResourceQueryHelper> REFERENCE!)

        if (_leaderboardService is null)
        {
            _serviceAvailable = false;
            if (HttpContextAccessor.HttpContext is { } httpContext)
            {
                httpContext.Response.StatusCode = 404;
            }

            NavManager.NavigateTo("/NotFound", replace: true);
            return;
        }

        _serviceAvailable = true;

        if (!_firstLoad && _previousGame == GameParam && _previousMap == MapParam &&
            _previousPlayers == PlayersParam)
        {
            return;
        }

        var mapChanged = _firstLoad || _previousGame != GameParam || _previousMap != MapParam;

        _firstLoad = false;
        _previousGame = GameParam;
        _previousMap = MapParam;
        _previousPlayers = PlayersParam;
        _entriesLoaded = false;

        _metadata = await _leaderboardService.GetLeaderboardMetadataAsync();

        if (_metadata.Games.Count == 0)
        {
            _hasLoaded = true;
            return;
        }

        // Resolve selected game
        if (GameParam is not null && Enum.TryParse<Reference.Game>(GameParam, true, out var parsedGame))
        {
            _selectedGame = _metadata.Games.FirstOrDefault(g => g.Game == parsedGame);
        }

        _selectedGame ??= _metadata.Games[0];

        // Resolve selected map
        if (MapParam is not null && int.TryParse(MapParam, out var parsedMapId))
        {
            _selectedMap = _selectedGame.Maps.FirstOrDefault(m => m.MapId == parsedMapId);
        }

        _selectedMap ??= _selectedGame.Maps.FirstOrDefault();

        // Populate sidebar menu
        MenuItems = new SharedLibraryCore.Dtos.SideContextMenuItems
        {
            MenuTitle = AppState.Loc("WEBFRONT_STATS_INDEX_CATEGORIES"),
            Items = _metadata.Games.SelectMany(game =>
            {
                var list = new List<SharedLibraryCore.Dtos.SideContextMenuItem>
                {
                    new()
                    {
                        IsSectionHeader = true,
                        Title = game.DisplayName,
                        Icon = "ph-game-controller"
                    }
                };

                list.AddRange(game.Maps.Select(map => new SharedLibraryCore.Dtos.SideContextMenuItem
                {
                    IsLink = true,
                    Title = map.MapName,
                    Reference = BuildFilterUrl(game.Game.ToString(), map.MapId.ToString(),
                        map.PlayerCounts.FirstOrDefault().ToString()),
                    IsActive = _selectedGame.Game == game.Game && _selectedMap?.MapId == map.MapId,
                    Icon = "ph-map-trifold"
                }));

                return list;
            }).ToList()
        };

        // Resolve selected player count
        if (_selectedMap is not null)
        {
            if (PlayersParam is not null && int.TryParse(PlayersParam, out var parsedPlayers) &&
                _selectedMap.PlayerCounts.Contains(parsedPlayers))
            {
                _selectedPlayerCount = parsedPlayers;
            }
            else
            {
                _selectedPlayerCount =
                    _selectedMap.PlayerCounts.FirstOrDefault();
            }
        }

        _hasLoaded = true;

        // Load map records for the marquee — only reload when game/map changes, not player count
        if (_selectedMap is not null && mapChanged)
        {
            _mapRecords = await _leaderboardService.GetMapRecordsAsync(_selectedGame.Game, _selectedMap.MapId);
            Random.Shared.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_mapRecords));
        }

        // Reset paging on filter change and fetch the first batch.
        _entries.Clear();
        _expandedMatches.Clear();
        _loadingMatches.Clear();
        _hasMore = true;
        await LoadBatch(InitialBatchSize);
    }

    private async Task LoadBatch(int count)
    {
        if (_leaderboardService is null || _selectedGame is null || _selectedMap is null) return;

        try
        {
            var response = await _leaderboardService.GetLeaderboardEntriesAsync(
                _selectedGame.Game,
                _selectedMap.MapId,
                _selectedPlayerCount,
                _entries.Count,
                count);

            _totalEntries = response.TotalCount;
            _entries.AddRange(response.Entries);
            _hasMore = response.Entries.Count >= count && _entries.Count < response.TotalCount;
            _entriesLoaded = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading zombie leaderboard entries");
            _hasMore = false;
        }
    }

    private async Task LoadMore()
    {
        if (_isLoadingMore || !_hasMore) return;
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

    private string BuildFilterUrl(string? game = null, string? map = null, string? players = null)
    {
        var g = game ?? GameParam ?? _selectedGame?.Game.ToString();
        var m = map ?? MapParam ?? _selectedMap?.MapId.ToString();
        var p = players ?? PlayersParam ?? _selectedPlayerCount.ToString();
        return $"/stats/zombies?game={g}&map={m}&players={p}";
    }

    private static string GetPlayerCountLabel(int count) => count switch
    {
        1 => "Solo",
        _ => $"{count} Players"
    };

    private static string GetGameImagePath(Reference.Game game) =>
        $"/images/zombies/{game.ToString().ToLowerInvariant()}.jpg";

    private async Task ToggleMatchDetail(int matchId)
    {
        if (_expandedMatches.ContainsKey(matchId))
        {
            _expandedMatches.Remove(matchId);
            return;
        }

        if (_matchHistoryService is null || _loadingMatches.Contains(matchId)) return;

        _loadingMatches.Add(matchId);
        _expandedMatches[matchId] = null;
        StateHasChanged();

        try
        {
            var detail = await _matchHistoryService.GetMatchDetailAsync(matchId);
            _expandedMatches[matchId] = detail;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load match detail for {MatchId}", matchId);
            _expandedMatches.Remove(matchId);
        }
        finally
        {
            _loadingMatches.Remove(matchId);
        }
    }
}
