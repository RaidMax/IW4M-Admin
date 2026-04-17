using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SharedLibraryCore.Dtos;
using WebfrontCore.Controllers.API.Models;

namespace WebfrontCore.Controllers.API
{
    /// <summary>
    /// Player statistics — advanced hit data, top-player leaderboards, chat search
    /// and context, and automated-penalty context.
    /// </summary>
    [ApiController]
    [Route("api/stats")]
    [Tags("Stats")]
    [Produces("application/json")]
    public class StatsController(
        ILogger<StatsController> logger,
        Core.Services.IWebfrontDataService dataService)
        : ControllerBase
    {
        /// <remarks>
        /// Returns detailed statistics for a single client — hit locations, weapon breakdowns,
        /// per-server performance, and encounter data. Filter to a single server with <c>serverId</c>
        /// or omit to get aggregate stats across all servers.
        /// </remarks>
        /// <param name="clientId">IW4MAdmin client identifier.</param>
        /// <param name="serverId">Optional server id to scope the stats to.</param>
        /// <param name="token">Request cancellation token.</param>
        /// <response code="200">Advanced stats returned.</response>
        /// <response code="404">Client has no stats, or an error occurred during lookup.</response>
        [HttpGet("{clientId:int}/advanced")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAdvancedStats(int clientId, [FromQuery] string? serverId,
            CancellationToken token = default)
        {
            try
            {
                var hitInfo = await dataService.GetClientStatisticsAsync(clientId, serverId);
                return Ok(hitInfo);
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        /// <remarks>
        /// Returns the top-ranked players by performance score. Filter to a single server with
        /// <c>serverId</c> or omit for the global leaderboard.
        /// </remarks>
        /// <param name="count">Page size (defaults to 25).</param>
        /// <param name="offset">Pagination offset.</param>
        /// <param name="serverId">Optional server id to scope the leaderboard to.</param>
        /// <response code="200">Top players returned.</response>
        [HttpGet("top")]
        [ProducesResponseType<TopStatsResponse>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTopPlayers([FromQuery] int count = 25, [FromQuery] int offset = 0,
            [FromQuery] string? serverId = null)
        {
            var response = await dataService.GetTopStatsAsync(new Models.TopStatsRequest
            {
                Count = count,
                Offset = offset,
                ServerId = serverId
            });
            return Ok(response);
        }

        /// <remarks>
        /// Returns per-server stats summaries for a single client — score, kills, deaths,
        /// time played, skill, and rank on each server the client has played on.
        /// </remarks>
        /// <param name="clientId">IW4MAdmin client identifier.</param>
        /// <response code="200">Stats returned.</response>
        /// <response code="400">Invalid client id.</response>
        /// <response code="404">Client has no stats on any server.</response>
        /// <response code="500">Unexpected server error.</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("{clientId:int}")]
        public async Task<IActionResult> ClientStats(int clientId)
        {
            if (clientId < 1 || !ModelState.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    Messages = [$"Client Id must be between 1 and {int.MaxValue}"]
                });
            }

            try
            {
                var result = await dataService.GetClientStatsAsync(clientId);

                if (result.Count == 0)
                {
                    return NotFound();
                }

                return Ok(result);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Could not get client stats for client id {ClientId}", clientId);

                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
                {
                    Messages = [e.Message]
                });
            }
        }

        /// <remarks>
        /// Returns the chat messages from a server around a given moment — typically the
        /// messages either side of an event (kill, penalty, report) to provide context.
        /// </remarks>
        /// <param name="serverId">Server identifier.</param>
        /// <param name="when">Unix timestamp (ms) to centre the context window on.</param>
        /// <response code="200">Context messages returned.</response>
        [HttpGet("message/context")]
        [ProducesResponseType<List<SharedLibraryCore.Dtos.Meta.Responses.MessageResponse>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMessageContext([FromQuery] string serverId, [FromQuery] long when)
        {
            var messages = await dataService.GetChatContextAsync(serverId, when);
            return Ok(messages);
        }

        /// <remarks>
        /// Full-text search over chat history. Supports filtering by server, client, date range,
        /// and message substring via the query object.
        /// </remarks>
        /// <param name="request">Search criteria and pagination.</param>
        /// <response code="200">Matching messages returned.</response>
        [HttpGet("message/search")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> SearchMessages([FromQuery] Stats.Dtos.ChatSearchQuery request)
        {
            var result = await dataService.SearchMessagesAsync(request);
            return Ok(result);
        }

        /// <remarks>
        /// Returns the automated-detection context for a penalty that was issued by anti-cheat
        /// (hit snapshots, angle changes, projected impact points, etc.). Used to render the
        /// "why was this player banned" page. Returns 404 if the penalty is not anti-cheat issued.
        /// </remarks>
        /// <param name="penaltyId">Penalty identifier.</param>
        /// <response code="200">Context returned.</response>
        /// <response code="403">Caller lacks <c>Permissions.BanManagementPage.Read</c>.</response>
        /// <response code="404">Penalty not found, or was not issued by anti-cheat.</response>
        [HttpGet("penalty/{penaltyId:int}/context")]
        [ProducesResponseType<List<Dictionary<string, string>>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Policy = "Permissions.BanManagementPage.Read")]
        public async Task<IActionResult> GetAutomatedPenaltyInfo(int penaltyId)
        {
            try
            {
                var info = await dataService.GetAutomatedPenaltyContextAsync(penaltyId);
                return Ok(info);
            }
            catch (Exception)
            {
                return NotFound();
            }
        }
    }
}
