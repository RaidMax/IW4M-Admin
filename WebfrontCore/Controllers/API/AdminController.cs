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
    [Route("api/[controller]")]
    public class AdminController : BaseController
    {
        private readonly IWebfrontDataService _dataService;

        public AdminController(IManager manager, IWebfrontDataService dataService) : base(manager)
        {
            _dataService = dataService;
        }

        [HttpGet("audit")]
        [Authorize(Policy = "Permissions.AuditPage.Read")]
        public async Task<ActionResult<System.Collections.Generic.IList<SharedLibraryCore.Dtos.AuditInfo>>> GetAuditLog([FromQuery] PaginationRequest request)
        {
            var auditItems = await _dataService.GetAuditLogAsync(request);
            return Ok(auditItems);
        }

        [HttpGet("bans")]
        [Authorize(Policy = "Permissions.BanManagementPage.Read")]
        public async Task<ActionResult<SharedLibraryCore.Helpers.ResourceQueryHelperResult<BanInfo>>> GetBans([FromQuery] BanInfoRequest request)
        {
            var results = await _dataService.GetBansAsync(request);
            return Ok(results);
        }

        [HttpGet("alerts")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<SharedLibraryCore.Alerts.Alert.AlertState>>> GetAlerts()
        {
            var alerts = await _dataService.GetAlertsAsync();
            return Ok(alerts);
        }

        [HttpPost("alerts/{id:guid}/dismiss")]
        [Authorize]
        public async Task<ActionResult> DismissAlert(Guid id)
        {
            await _dataService.DismissAlertAsync(id);
            return Ok();
        }
        
        [HttpPost("alerts/dismiss/all")]
        [Authorize]
        public async Task<ActionResult> DismissAllAlerts()
        {
            await _dataService.DismissAllAlertsAsync();
            return Ok();
        }

        [HttpGet("reports")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<ServerReportsInfo>>> GetReports()
        {
            var reports = await _dataService.GetReportsAsync();
            return Ok(reports);
        }
    }
}
