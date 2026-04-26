using System.Net.Mime;
using Data.Models;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Controllers.API;

/// <summary>
/// Zombie match data — leaderboards, map records, and per-player match history.
/// All endpoints require the Zombie Stats Premium plugin; without it every route returns 404.
/// </summary>
[ApiController]
[Route("api/zombie")]
[Tags("Zombie Stats")]
[Produces(MediaTypeNames.Application.Json)]
public class ZombieStatsController(
    ILogger<ZombieStatsController> logger,
    IServiceProvider serviceProvider) : ControllerBase
{
    private readonly IZombieLeaderboardService? _leaderboardService =
        serviceProvider.GetService<IZombieLeaderboardService>();

    private readonly IZombieMatchHistoryService? _matchHistoryService =
        serviceProvider.GetService<IZombieMatchHistoryService>();

    private readonly IZombieLiveMatchService? _liveMatchService =
        serviceProvider.GetService<IZombieLiveMatchService>();

    /// <remarks>
    /// Returns the games, maps, and player counts that can be used to filter leaderboard entries.
    /// Use the values returned here to populate the <c>game</c>, <c>mapId</c>, and <c>playerCount</c>
    /// query parameters on <c>GET /api/zombie/leaderboard</c>.
    /// </remarks>
    /// <response code="200">Metadata returned.</response>
    /// <response code="404">Zombie Stats Premium plugin is not installed.</response>
    [HttpGet("leaderboard/metadata")]
    [ProducesResponseType<ZombieLeaderboardMetadata>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLeaderboardMetadata()
    {
        if (_leaderboardService is null)
        {
            return NotFound();
        }

        var metadata = await _leaderboardService.GetLeaderboardMetadataAsync();
        return Ok(metadata);
    }

    /// <remarks>
    /// Returns ranked entries for a game/map/player-count, sorted by highest round descending.
    /// Page size is capped at 100.
    /// </remarks>
    /// <param name="game">Game code (e.g. <c>T4</c>, <c>T5</c>, <c>T6</c>).</param>
    /// <param name="mapId">Map identifier, from <c>/api/zombie/leaderboard/metadata</c>.</param>
    /// <param name="playerCount">Player count filter, from metadata.</param>
    /// <param name="offset">Pagination offset. Defaults to 0.</param>
    /// <param name="count">Page size. Defaults to 25, capped at 100.</param>
    /// <response code="200">Leaderboard page returned.</response>
    /// <response code="404">Zombie Stats Premium plugin is not installed.</response>
    [HttpGet("leaderboard")]
    [ProducesResponseType<ZombieLeaderboardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLeaderboardEntries(
        [FromQuery] Reference.Game game,
        [FromQuery] int mapId,
        [FromQuery] int playerCount,
        [FromQuery] int offset = 0,
        [FromQuery] int count = 25)
    {
        if (_leaderboardService is null)
        {
            return NotFound();
        }

        count = Math.Min(count, 100);
        var response = await _leaderboardService.GetLeaderboardEntriesAsync(game, mapId, playerCount, offset, count);
        return Ok(response);
    }

    /// <remarks>
    /// Returns notable records for a map across all player counts (highest round, most kills,
    /// best economy, etc.).
    /// </remarks>
    /// <param name="game">Game code (e.g. <c>T4</c>, <c>T5</c>, <c>T6</c>).</param>
    /// <param name="mapId">Map identifier, from <c>/api/zombie/leaderboard/metadata</c>.</param>
    /// <response code="200">Records returned (may be empty).</response>
    /// <response code="404">Zombie Stats Premium plugin is not installed.</response>
    [HttpGet("leaderboard/records")]
    [ProducesResponseType<List<ZombieMapStatRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMapRecords(
        [FromQuery] Reference.Game game,
        [FromQuery] int mapId)
    {
        if (_leaderboardService is null)
        {
            return NotFound();
        }

        var records = await _leaderboardService.GetMapRecordsAsync(game, mapId);
        return Ok(records);
    }

    /// <remarks>
    /// Returns all players' stats, per-round breakdowns, and the full event timeline for one match.
    /// </remarks>
    /// <param name="matchId">Match identifier, from a leaderboard or match-history entry.</param>
    /// <response code="200">Match detail returned.</response>
    /// <response code="404">Match not found, or Zombie Stats Premium is not installed.</response>
    [HttpGet("match/{matchId:int}")]
    [ProducesResponseType<ZombieMatchDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMatchDetail(int matchId)
    {
        if (_matchHistoryService is null)
        {
            return NotFound();
        }

        try
        {
            var detail = await _matchHistoryService.GetMatchDetailAsync(matchId);
            return detail is null ? NotFound() : Ok(detail);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not get match detail for match {MatchId}", matchId);
            return NotFound();
        }
    }

    /// <remarks>
    /// Returns a player's recent zombie matches with round breakdowns and event timelines.
    /// Page size is capped at 50.
    /// </remarks>
    /// <param name="clientId">Player's IW4MAdmin client ID.</param>
    /// <param name="serverEndpoint">Optional server endpoint (<c>ip:port</c>) to filter by.</param>
    /// <param name="offset">Pagination offset. Defaults to 0.</param>
    /// <param name="count">Page size. Defaults to 10, capped at 50.</param>
    /// <response code="200">Match history returned (may be empty).</response>
    /// <response code="404">Zombie Stats Premium plugin is not installed.</response>
    [HttpGet("client/{clientId:int}/history")]
    [ProducesResponseType<List<ZombieMatchHistoryMatch>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlayerMatchHistory(
        int clientId,
        [FromQuery] string? serverEndpoint = null,
        [FromQuery] int offset = 0,
        [FromQuery] int count = 10)
    {
        if (_matchHistoryService is null)
        {
            return NotFound();
        }

        count = Math.Min(count, 50);
        var history = await _matchHistoryService.GetPlayerMatchHistoryAsync(clientId, serverEndpoint, offset, count);
        return Ok(history);
    }

    /// <remarks>
    /// Returns a live snapshot of an in-progress zombie match for a server: per-player
    /// current/cumulative stats, recent events, and rounds completed so far. Returns 404
    /// when the server has no active match. Designed for periodic polling (recommended
    /// interval: 5s, matching the standard scoreboard).
    /// </remarks>
    /// <param name="serverId">Server identifier (typically <c>ip:port</c>).</param>
    /// <response code="200">Live snapshot returned.</response>
    /// <response code="404">Server has no active match, or Zombie Stats Premium is not installed.</response>
    [HttpGet("server/{serverId}/live-match")]
    [ProducesResponseType<ZombieLiveMatchSnapshot>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLiveMatchSnapshot(string serverId)
    {
        if (_liveMatchService is null)
        {
            return NotFound();
        }

        var snapshot = await _liveMatchService.GetLiveMatchSnapshotAsync(serverId);
        return snapshot is null ? NotFound() : Ok(snapshot);
    }
}
