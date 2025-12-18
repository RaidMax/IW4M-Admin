using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;
using Data.Models;
using Microsoft.AspNetCore.Authorization;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class PenaltyController : BaseController
    {
        private readonly IWebfrontDataService _dataService;

        public PenaltyController(IManager manager, IWebfrontDataService dataService) : base(manager)
        {
            _dataService = dataService;
        }

        [HttpGet]
        public async Task<ActionResult<IList<PenaltyInfo>>> GetPenalties(int offset = 0, int count = 30, EFPenalty.PenaltyType showOnly = EFPenalty.PenaltyType.Any, bool ignoreAutomated = true)
        {
            var penalties = await _dataService.GetPenaltiesAsync(offset, count, showOnly, ignoreAutomated);
            return Ok(penalties);
        }

        [HttpPost("unban/{targetId}")]
        [Authorize]
        public async Task<IActionResult> UnbanAsync(int targetId, [FromBody] UnbanRequest request)
        {
            try
            {
               var message = await _dataService.UnbanClientAsync(targetId, request.Reason);
               return Ok(new { message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class UnbanRequest
    {
        public string Reason { get; set; }
    }
}
