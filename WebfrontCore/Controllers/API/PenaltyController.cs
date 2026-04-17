using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
using WebfrontCore.Controllers.API.Models;
using WebfrontCore.Core.Auth;

namespace WebfrontCore.Controllers.API
{
    /// <summary>
    /// Penalty listing and unban operations. Bans, kicks, warnings, mutes and related records.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Penalties")]
    [Produces("application/json")]
    public class PenaltyController(IManager manager, IWebfrontDataService dataService) : BaseController(manager)
    {
        /// <remarks>
        /// Returns a paginated list of penalties (bans, kicks, warnings, mutes, etc.), newest first.
        /// <c>showOnly</c> filters to a single penalty type; <c>ignoreAutomated</c> hides penalties
        /// issued by automated systems (e.g. anti-cheat) when <c>true</c>.
        /// </remarks>
        /// <param name="offset">Pagination offset.</param>
        /// <param name="count">Page size (defaults to 30).</param>
        /// <param name="showOnly">Filter to a specific penalty type (<c>Any</c> returns all).</param>
        /// <param name="ignoreAutomated">Hide automated penalties when <c>true</c> (default).</param>
        /// <response code="200">Penalty list returned.</response>
        /// <response code="403">Caller lacks <c>Permissions.Penalty.Read</c>.</response>
        [Authorize(Policy = $"Permissions.{nameof(WebfrontEntity.Penalty)}.{nameof(WebfrontPermission.Read)}")]
        [ProducesResponseType<IList<PenaltyInfo>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IList<PenaltyInfo>>> GetPenalties(int offset = 0, int count = 30,
            EFPenalty.PenaltyType showOnly = EFPenalty.PenaltyType.Any, bool ignoreAutomated = true)
        {
            var penalties = await dataService.GetPenaltiesAsync(new PenaltyRequest
            {
                Offset = offset,
                Count = count,
                ShowOnly = showOnly,
                IgnoreAutomated = ignoreAutomated
            });
            return Ok(penalties);
        }

        /// <remarks>
        /// Removes an active ban for the target client and records the unban in the audit log
        /// with the supplied reason. Permission to unban is enforced by the underlying data service
        /// based on the caller's level relative to the banning admin.
        /// </remarks>
        /// <param name="targetId">Client id of the banned player to unban.</param>
        /// <param name="request">Unban reason.</param>
        /// <response code="200">Unban succeeded — descriptive message returned.</response>
        /// <response code="400">Unban refused (permission, no active ban, etc.) — reason in body.</response>
        /// <response code="401">Caller is not authenticated.</response>
        [HttpPost("unban/{targetId:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
