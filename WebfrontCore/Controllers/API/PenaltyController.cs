using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
using System.Linq;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class PenaltyController : BaseController
    {
        public PenaltyController(IManager manager) : base(manager)
        {
        }

        [HttpGet]
        public async Task<ActionResult<IList<PenaltyInfo>>> GetPenalties(int offset = 0, int count = 30, EFPenalty.PenaltyType showOnly = EFPenalty.PenaltyType.Any, bool ignoreAutomated = true)
        {
            var penalties = await Manager.GetPenaltyService().GetRecentPenalties(count, offset, showOnly, ignoreAutomated);
            // Permission filtering logic from PenaltyListViewComponent
            if (Client.Level == Data.Models.Client.EFClient.Permission.User)
            {
                 // Filter sensitive info if needed, though GetRecentPenalties might return DTOs already.
                 // The ViewComponent logic was:
                 // penalties = User.Identity.IsAuthenticated ? penalties : penalties.Where(p => !p.Sensitive).ToList();
                 // Here Client.ClientId is set if authenticated.
                 // Actually BaseController sets Client.
                 
                 // Wait, logic in ViewComponent:
                 // penalties = User.Identity.IsAuthenticated ? penalties : penalties.Where(p => !p.Sensitive).ToList();
                 
                 // In API, if not authenticated (Client.ClientId is 0 or -1?), we should filter.
                 // But BaseController usually requires auth for Client to be populated?
                 // Let's check BaseController. But assume we need to replicate the logic.
            }
            
            // Check if user is authenticated. BaseController has 'Authorized' property or similar?
            // BaseController sets Client from claims. if not logged in, Client is probably a default or null.
            // Let's rely on User.Identity.IsAuthenticated from Controller context.
            
            if (!User.Identity.IsAuthenticated)
            {
                // Filter sensitive
                // PenaltyInfo has a Sensitive property.
                // We need to return a new list if filtering.
                 var filtered = new List<PenaltyInfo>();
                 foreach(var p in penalties)
                 {
                     if (!p.Sensitive) filtered.Add(p);
                 }
                 return Ok(filtered);
            }

            return Ok(penalties);
        }

        [HttpPost("unban/{targetId}")]
        public async Task<IActionResult> UnbanAsync(int targetId, [FromBody] UnbanRequest request)
        {
            var client = await Manager.GetClientService().Get(targetId);
            if (client == null) return NotFound();

            if (!Authorized) return Unauthorized();

            var server = Manager.GetServers().First();
            Client.CurrentServer = server;
            var unbanEvent = client.Unban(request.Reason, Client);
            
            // Wait for the event to complete and get its result
            await unbanEvent.WaitAsync();
            
            if (unbanEvent.Failed)
            {
                var errorMessage = unbanEvent.Output.Count > 0 
                    ? string.Join(" ", unbanEvent.Output) 
                    : "Unban failed";
                return BadRequest(new { message = errorMessage });
            }
            
            var successMessage = unbanEvent.Output.Count > 0 
                ? string.Join(" ", unbanEvent.Output) 
                : "Client unbanned successfully";
            
            return Ok(new { message = successMessage });
        }
    }

    public class UnbanRequest
    {
        public string Reason { get; set; }
    }
}
