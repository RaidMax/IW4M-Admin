using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;
using WebfrontCore.Core.QueryHelpers.Models;
using WebfrontCore.Components.Features.Servers.Models;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class AdminController(IManager manager, IWebfrontDataService dataService) : BaseController(manager)
    {
        [HttpGet("audit")]
        [Authorize(Policy = "Permissions.AuditPage.Read")]
        public async Task<ActionResult<IList<AuditInfo>>> GetAuditLog([FromQuery] PaginationRequest request)
        {
            var auditItems = await dataService.GetAuditLogAsync(request);
            return Ok(auditItems);
        }

        [HttpGet("bans")]
        [Authorize(Policy = "Permissions.BanManagementPage.Read")]
        public async Task<ActionResult<SharedLibraryCore.Helpers.ResourceQueryHelperResult<BanInfo>>> GetBans(
            [FromQuery] BanInfoRequest request)
        {
            var results = await dataService.GetBansAsync(request);
            return Ok(results);
        }

        [HttpGet("alerts")]
        public async Task<ActionResult<IEnumerable<SharedLibraryCore.Alerts.Alert.AlertState>>> GetAlerts()
        {
            var alerts = await dataService.GetAlertsAsync();
            return Ok(alerts);
        }

        [HttpPost("alerts/{id:guid}/dismiss")]
        public async Task<ActionResult> DismissAlert(Guid id)
        {
            await dataService.DismissAlertAsync(id);
            return Ok();
        }

        [HttpPost("alerts/dismiss/all")]
        public async Task<ActionResult> DismissAllAlerts()
        {
            await dataService.DismissAllAlertsAsync();
            return Ok();
        }

        [HttpGet("reports")]
        public async Task<ActionResult<IEnumerable<ServerReportsInfo>>> GetReports()
        {
            var reports = await dataService.GetReportsAsync();
            return Ok(reports);
        }
    }
}
