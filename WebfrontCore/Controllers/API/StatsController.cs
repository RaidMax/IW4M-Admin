using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SharedLibraryCore.Dtos;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/stats")]
    public class StatsController(
        ILogger<StatsController> logger,
        Core.Services.IWebfrontDataService dataService)
        : ControllerBase
    {
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

        [HttpGet("top")]
        [ProducesResponseType(StatusCodes.Status200OK)]
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

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

        [HttpGet("message/context")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMessageContext([FromQuery] string serverId, [FromQuery] long when)
        {
            var messages = await dataService.GetChatContextAsync(serverId, when);
            return Ok(messages);
        }

        [HttpGet("message/search")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> SearchMessages([FromQuery] Stats.Dtos.ChatSearchQuery request)
        {
            var result = await dataService.SearchMessagesAsync(request);
            return Ok(result);
        }

        [HttpGet("penalty/{penaltyId:int}/context")]
        [ProducesResponseType(StatusCodes.Status200OK)]
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
