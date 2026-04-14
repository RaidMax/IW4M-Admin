using Data.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
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
    private Virtualize<ZombieLeaderboardEntry>? _virtualizeComponent;
    private SharedLibraryCore.Dtos.SideContextMenuItems? MenuItems { get; set; }
    private readonly Dictionary<int, SharedLibraryCore.Interfaces.ZombieMatchDetail?> _expandedMatches = new();
    private readonly HashSet<int> _loadingMatches = new();
    private List<ZombieMapStatRecord> _mapRecords = [];

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

        if (_virtualizeComponent is not null)
        {
            await _virtualizeComponent.RefreshDataAsync();
        }
    }

    private const int BatchSize = 50;

    private async ValueTask<ItemsProviderResult<ZombieLeaderboardEntry>> LoadEntries(
        ItemsProviderRequest request)
    {
        if (_leaderboardService is null || _selectedGame is null || _selectedMap is null)
        {
            return new ItemsProviderResult<ZombieLeaderboardEntry>([], 0);
        }

        try
        {
            var response = await _leaderboardService.GetLeaderboardEntriesAsync(
                _selectedGame.Game,
                _selectedMap.MapId,
                _selectedPlayerCount,
                request.StartIndex,
                Math.Max(BatchSize, request.Count));

            _totalEntries = response.TotalCount;
            _entriesLoaded = true;
            return new ItemsProviderResult<ZombieLeaderboardEntry>(response.Entries, response.TotalCount);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading zombie leaderboard entries");
            return new ItemsProviderResult<ZombieLeaderboardEntry>([], _totalEntries);
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
