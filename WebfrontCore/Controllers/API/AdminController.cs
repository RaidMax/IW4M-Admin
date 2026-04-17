using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.OpenApi;
using WebfrontCore.Core.Services;
using WebfrontCore.Core.QueryHelpers.Models;
using WebfrontCore.Components.Features.Servers.Models;

namespace WebfrontCore.Controllers.API
{
    /// <summary>
    /// Administrative endpoints — audit log, bans list, alerts, and reports.
    /// All routes require an authenticated session; individual endpoints may require
    /// additional permission policies.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    [Tags("Admin")]
    [Produces("application/json")]
    [TagDescription(
        "Administrative endpoints — audit log, bans list, alerts, and reports. " +
        "All routes require an authenticated session; individual endpoints may require additional permission policies.")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public class AdminController(IManager manager, IWebfrontDataService dataService) : BaseController(manager)
    {
        /// <remarks>
        /// Returns audit log entries filtered by the supplied criteria. Audit entries record
        /// administrative actions (bans, kicks, command execution, etc.) with the originating
        /// admin, target, and timestamp.
        /// </remarks>
        /// <param name="request">Filter criteria (origin, target, action type, date range, pagination).</param>
        /// <response code="200">Matching audit entries returned (may be empty).</response>
        /// <response code="403">Caller lacks <c>Permissions.AuditPage.Read</c>.</response>
        [HttpGet("audit")]
        [Authorize(Policy = "Permissions.AuditPage.Read")]
        [ProducesResponseType<IList<AuditInfo>>(StatusCodes.Status200OK)]
        public async Task<ActionResult<IList<AuditInfo>>> GetAuditLog([FromQuery] AuditFilterRequest request)
        {
            var auditItems = await dataService.GetAuditLogAsync(request);
            return Ok(auditItems);
        }

        /// <remarks>
        /// Returns paginated active ban records. Each entry includes the target client, the
        /// issuing admin, reason, and ban metadata.
        /// </remarks>
        /// <param name="request">Pagination and filter criteria.</param>
        /// <response code="200">Ban list returned.</response>
        /// <response code="403">Caller lacks <c>Permissions.BanManagementPage.Read</c>.</response>
        [HttpGet("bans")]
        [Authorize(Policy = "Permissions.BanManagementPage.Read")]
        [ProducesResponseType<SharedLibraryCore.Helpers.ResourceQueryHelperResult<BanInfo>>(StatusCodes.Status200OK)]
        public async Task<ActionResult<SharedLibraryCore.Helpers.ResourceQueryHelperResult<BanInfo>>> GetBans(
            [FromQuery] BanInfoRequest request)
        {
            var results = await dataService.GetBansAsync(request);
            return Ok(results);
        }

        /// <remarks>
        /// Returns the caller's active alerts (system notifications, warnings, and undismissed
        /// events). Alerts are scoped to the authenticated user.
        /// </remarks>
        /// <response code="200">Alerts returned (may be empty).</response>
        [HttpGet("alerts")]
        [ProducesResponseType<IEnumerable<SharedLibraryCore.Alerts.Alert.AlertState>>(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<SharedLibraryCore.Alerts.Alert.AlertState>>> GetAlerts()
        {
            var alerts = await dataService.GetAlertsAsync();
            return Ok(alerts);
        }

        /// <remarks>
        /// Dismisses a single alert for the current user. Dismissed alerts do not reappear
        /// in <c>GET /api/admin/alerts</c>.
        /// </remarks>
        /// <param name="id">Alert identifier.</param>
        /// <response code="200">Alert dismissed.</response>
        [HttpPost("alerts/{id:guid}/dismiss")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> DismissAlert(Guid id)
        {
            await dataService.DismissAlertAsync(id);
            return Ok();
        }

        /// <remarks>
        /// Dismisses every active alert for the current user in a single call.
        /// </remarks>
        /// <response code="200">All alerts dismissed.</response>
        [HttpPost("alerts/dismiss/all")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> DismissAllAlerts()
        {
            await dataService.DismissAllAlertsAsync();
            return Ok();
        }

        /// <remarks>
        /// Returns open player reports grouped by server. Used by the reports dashboard to
        /// show admins what has been flagged recently.
        /// </remarks>
        /// <response code="200">Reports returned (may be empty).</response>
        /// <response code="403">Caller lacks <c>Permissions.Penalty.Read</c>.</response>
        [HttpGet("reports")]
        [Authorize(Policy = "Permissions.Penalty.Read")]
        [ProducesResponseType<IEnumerable<ServerReportsInfo>>(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ServerReportsInfo>>> GetReports()
        {
            var reports = await dataService.GetReportsAsync();
            return Ok(reports);
        }
    }
}
