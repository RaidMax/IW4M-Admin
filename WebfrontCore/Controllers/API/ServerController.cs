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
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ServerController(IManager manager, IWebfrontDataService dataService) : BaseController(manager)
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServerInfo>>> GetServers([FromQuery] Reference.Game? game = null,
            CancellationToken token = default)
        {
            var servers = await dataService.GetServersAsync(game);
            return Ok(servers);
        }

        [HttpGet("{id}")]
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

        [HttpGet("{id}/scoreboard")]
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
        
        [HttpGet("{id}/history")]
        public async Task<ActionResult<IEnumerable<ClientCountSnapshot>>> GetClientHistory(string id)
        {
            var history = await dataService.GetClientHistoryAsync(id);
            return Ok(history);
        }
        
        /// <summary>
        /// Get available RCon/Event parsers for server configuration
        /// </summary>
        [HttpGet("parsers")]
        public ActionResult<IEnumerable<string>> GetAvailableParsers()
        {
            var parsers = dataService.GetAvailableParsers();
            return Ok(parsers);
        }
        
        /// <summary>
        /// Add a new server dynamically at runtime
        /// </summary>
        [HttpPost]
        [Authorize(Roles = nameof(EFClient.Permission.Owner))]
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
        
        /// <summary>
        /// Remove a server dynamically at runtime
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = nameof(EFClient.Permission.Owner))]
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
