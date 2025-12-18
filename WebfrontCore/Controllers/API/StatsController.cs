using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using Stats.Config;
using Stats.Dtos;
using WebfrontCore.Controllers.API.Models;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/stats")]
    public class StatsController : ControllerBase
    {
        private readonly WebfrontCore.Core.Services.IWebfrontDataService _dataService;
        private readonly ILogger<StatsController> _logger;

        public StatsController(ILogger<StatsController> logger, WebfrontCore.Core.Services.IWebfrontDataService dataService)
        {
            _logger = logger;
            _dataService = dataService;
        }

        [HttpGet("{clientId:int}/advanced")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAdvancedStats(int clientId, [FromQuery] string? serverId, CancellationToken token = default)
        {
            try
            {
                var hitInfo = await _dataService.GetClientStatisticsAsync(clientId, serverId);
                return Ok(hitInfo);
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        [HttpGet("top")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTopPlayers([FromQuery] int count = 25, [FromQuery] int offset = 0, [FromQuery] string? serverId = null)
        {
             var response = await _dataService.GetTopStatsAsync(count, offset, serverId);
             return Ok(response);
        }

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("{clientId}")]
        public async Task<IActionResult> ClientStats(int clientId)
        {
            if (clientId < 1 || !ModelState.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    Messages = new[] { $"Client Id must be between 1 and {int.MaxValue}" }
                });
            }

            try
            {
                var result = await _dataService.GetClientStatsAsync(clientId);

                if (result.Count == 0)
                {
                    return NotFound();
                }

                return Ok(result);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Could not get client stats for client id {clientId}", clientId);

                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
                {
                    Messages = new[] { e.Message }
                });
            }
        }

        [HttpGet("message/context")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMessageContext([FromQuery] string serverId, [FromQuery] long when)
        {
            var messages = await _dataService.GetChatContextAsync(serverId, when);
            return Ok(messages);
        }

        [HttpGet("penalty/{penaltyId}/context")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Policy = "Permissions.BanManagementPage.Read")]
        public async Task<IActionResult> GetAutomatedPenaltyInfo(int penaltyId)
        {
            try
            {
                 // The DataService returns List<Dictionary<string, string>>
                 var info = await _dataService.GetAutomatedPenaltyContextAsync(penaltyId);
                 return Ok(info);
            }
            catch (Exception)
            {
                 // DataService throws if penalty not found or something
                 return NotFound();
            }
        }
    }
}
