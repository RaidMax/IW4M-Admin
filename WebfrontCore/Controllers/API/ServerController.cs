using Data.Models;
using Data.Models.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Components.Features.Servers.Models;
using WebfrontCore.Controllers.API.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Controllers.API
{
    /// <summary>
    /// Monitored game servers — live status, scoreboards, client history, and
    /// dynamic add/remove at runtime (Owner only).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Servers")]
    [Produces("application/json")]
    public class ServerController(IManager manager, IWebfrontDataService dataService) : BaseController(manager)
    {
        /// <remarks>
        /// Returns the list of currently monitored game servers with live status (map, gametype,
        /// player count, connection state, and any plugin-contributed fields such as
        /// <c>zombieRoundNumber</c>). Optionally filter to a single game.
        /// </remarks>
        /// <param name="game">Optional game filter (e.g. <c>IW4</c>, <c>T6</c>).</param>
        /// <param name="token">Request cancellation token.</param>
        /// <response code="200">Servers returned.</response>
        [HttpGet]
        [ProducesResponseType<IEnumerable<ServerInfo>>(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ServerInfo>>> GetServers([FromQuery] Reference.Game? game = null,
            CancellationToken token = default)
        {
            var servers = await dataService.GetServersAsync(game);
            return Ok(servers);
        }

        /// <remarks>
        /// Returns full live info for a single server. The id is the server's endpoint
        /// identifier (<c>ip:port</c> or the configured id).
        /// </remarks>
        /// <param name="id">Server identifier.</param>
        /// <response code="200">Server info returned.</response>
        /// <response code="404">No server with that id is currently monitored.</response>
        [HttpGet("{id}")]
        [ProducesResponseType<ServerInfo>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ServerInfo>> GetServer(string id)
        {
            try
            {
                var server = await dataService.GetServer(id);
                if (server == null) return NotFound();
                return Ok(server);
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        /// <remarks>
        /// Returns the current scoreboard — connected players grouped by team with score,
        /// kills, deaths, ping, and time-on-server.
        /// </remarks>
        /// <param name="id">Server identifier.</param>
        /// <response code="200">Scoreboard returned.</response>
        /// <response code="404">Server not monitored, or scoreboard unavailable.</response>
        [HttpGet("{id}/scoreboard")]
        [ProducesResponseType<ScoreboardInfo>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ScoreboardInfo>> GetScoreboard(string id)
        {
            try
            {
                var scoreboard = await dataService.GetServerScoreboardAsync(id);
                return Ok(scoreboard);
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        /// <remarks>
        /// Returns a time-series of client-count snapshots for the server, used to render
        /// the population-over-time chart on the server detail page.
        /// </remarks>
        /// <param name="id">Server identifier.</param>
        /// <response code="200">Snapshots returned.</response>
        [HttpGet("{id}/history")]
        [ProducesResponseType<IEnumerable<ClientCountSnapshot>>(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ClientCountSnapshot>>> GetClientHistory(string id)
        {
            var history = await dataService.GetClientHistoryAsync(id);
            return Ok(history);
        }

        /// <remarks>
        /// Returns the names of every RCon/Event parser plugin currently loaded. Used to
        /// populate the "Parser" dropdown when adding a new server.
        /// </remarks>
        /// <response code="200">Parser names returned.</response>
        [HttpGet("parsers")]
        [ProducesResponseType<IEnumerable<string>>(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<string>> GetAvailableParsers()
        {
            var parsers = dataService.GetAvailableParsers();
            return Ok(parsers);
        }

        /// <remarks>
        /// Adds a new game server to the monitored set at runtime. The server is validated and
        /// an RCon connection attempted before it is accepted. If <c>persistToConfiguration</c>
        /// is set on the request, the server is also written to <c>IW4MAdminSettings.json</c>
        /// so it survives a restart.
        /// </remarks>
        /// <param name="request">Server connection details (IP, port, rcon password, parser, etc.).</param>
        /// <param name="token">Request cancellation token.</param>
        /// <response code="200">Server added — response contains the assigned server id.</response>
        /// <response code="400">Request invalid (bad port, missing fields, validation failure).</response>
        /// <response code="403">Caller is not the Owner.</response>
        /// <response code="409">A server with the same endpoint is already monitored.</response>
        /// <response code="500">RCon handshake failed or unexpected server error.</response>
        [HttpPost]
        [Authorize(Roles = nameof(EFClient.Permission.Owner))]
        [ProducesResponseType<AddServerResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AddServerResponse>> AddServer(
            [FromBody] AddServerRequest request,
            CancellationToken token = default)
        {
            try
            {
                var response = await dataService.AddServerAsync(request, token);

                if (response == null)
                {
                    return Conflict($"Server {request.IPAddress}:{request.Port} is already being monitored");
                }

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return Problem(
                    detail: ex.Message,
                    title: "Failed to add server",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        /// <remarks>
        /// Stops monitoring the specified server. By default the change is in-memory only —
        /// set <c>persistToConfiguration=true</c> to also remove the server from
        /// <c>IW4MAdminSettings.json</c> so it does not reappear after restart.
        /// </remarks>
        /// <param name="id">Server identifier.</param>
        /// <param name="persistToConfiguration">Also remove the server from the on-disk configuration when <c>true</c>.</param>
        /// <param name="token">Request cancellation token.</param>
        /// <response code="204">Server removed.</response>
        /// <response code="403">Caller is not the Owner.</response>
        /// <response code="404">No server with that id is currently monitored.</response>
        /// <response code="500">Unexpected server error.</response>
        [HttpDelete("{id}")]
        [Authorize(Roles = nameof(EFClient.Permission.Owner))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> RemoveServer(
            string id,
            [FromQuery] bool persistToConfiguration = false,
            CancellationToken token = default)
        {
            try
            {
                var success = await dataService.RemoveServerAsync(id, persistToConfiguration, token);

                if (!success)
                {
                    return NotFound($"Server {id} not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return Problem(
                    detail: ex.Message,
                    title: "Failed to remove server",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
