using Data.Models;
using Data.Models.Client.Stats;
using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Components.Features.Servers.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatusController : BaseController
    {
        private readonly IWebfrontDataService _dataService;

        public StatusController(IManager manager, IWebfrontDataService dataService) : base(manager)
        {
            _dataService = dataService;
        }

        [HttpGet]
        public async Task<ActionResult<IW4MAdminInfo>> GetStatus([FromQuery] Reference.Game? game = null,
            CancellationToken cancellationToken = default)
        {
            var status = await _dataService.GetStatusAsync(game);
            return Ok(status);
        }

        [HttpGet("servers")]
        public async Task<ActionResult<IEnumerable<ServerInfo>>> GetServers([FromQuery] Reference.Game? game = null,
            CancellationToken token = default)
        {
            var servers = await _dataService.GetServersAsync(game);
            return Ok(servers);
        }

        [HttpGet("servers/{id}")]
        public async Task<ActionResult<ServerInfo>> GetServer(string id)
        {
            try
            {
                var server = await _dataService.GetServer(id);
                return Ok(server);
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        [HttpGet("server/{serverId}/scoreboard")]
        public async Task<ActionResult<ScoreboardInfo>> GetScoreboard(string serverId)
        {
            try
            {
                var scoreboard = await _dataService.GetServerScoreboardAsync(serverId);
                return Ok(scoreboard);
            }
            catch (Exception)
            {
                return NotFound();
            }
        }
    }
}
