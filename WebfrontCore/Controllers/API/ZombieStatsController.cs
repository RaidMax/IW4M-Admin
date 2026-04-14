using Data.Models;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Controllers.API;

[ApiController]
[Route("api/zombie")]
public class ZombieStatsController(
    ILogger<ZombieStatsController> logger,
    IServiceProvider serviceProvider) : ControllerBase
{
    private readonly IZombieLeaderboardService? _leaderboardService =
        serviceProvider.GetService<IZombieLeaderboardService>();

    private readonly IZombieMatchHistoryService? _matchHistoryService =
        serviceProvider.GetService<IZombieMatchHistoryService>();

    [HttpGet("leaderboard/metadata")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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

    [HttpGet("leaderboard")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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

    [HttpGet("leaderboard/records")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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

    [HttpGet("match/{matchId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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

    [HttpGet("client/{clientId:int}/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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
}
