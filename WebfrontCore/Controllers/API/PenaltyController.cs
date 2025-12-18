using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;
using Data.Models;
using Microsoft.AspNetCore.Authorization;

using WebfrontCore.Controllers.API.Models;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class PenaltyController(IManager manager, IWebfrontDataService dataService) : BaseController(manager)
    {
        [HttpGet]
        public async Task<ActionResult<IList<PenaltyInfo>>> GetPenalties(int offset = 0, int count = 30, EFPenalty.PenaltyType showOnly = EFPenalty.PenaltyType.Any, bool ignoreAutomated = true)
        {
            var penalties = await dataService.GetPenaltiesAsync(offset, count, showOnly, ignoreAutomated);
            return Ok(penalties);
        }

        [HttpPost("unban/{targetId}")]
        [Authorize]
        public async Task<IActionResult> UnbanAsync(int targetId, [FromBody] UnbanRequest request)
        {
            try
            {
               var message = await dataService.UnbanClientAsync(targetId, request.Reason);
               return Ok(new { message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
