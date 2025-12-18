using Data.Models;
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
    }
}
