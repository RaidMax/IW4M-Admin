using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using WebfrontCore.QueryHelpers.Models;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : BaseController
    {
        private readonly IAuditInformationRepository _auditInformationRepository;
        private readonly IResourceQueryHelper<BanInfoRequest, BanInfo> _banInfoQueryHelper;

        private readonly IAlertManager _alertManager;

        public AdminController(IManager manager, IAuditInformationRepository auditInformationRepository,
            IResourceQueryHelper<BanInfoRequest, BanInfo> banInfoQueryHelper, IAlertManager alertManager) : base(manager)
        {
            _auditInformationRepository = auditInformationRepository;
            _banInfoQueryHelper = banInfoQueryHelper;
            _alertManager = alertManager;
        }

        [HttpGet("audit")]
        [Authorize(Policy = "Permissions.AuditPage.Read")]
        public async Task<ActionResult<System.Collections.Generic.IList<SharedLibraryCore.Dtos.AuditInfo>>> GetAuditLog([FromQuery] PaginationRequest request)
        {
            var auditItems = await _auditInformationRepository.ListAuditInformation(request);
            return Ok(auditItems);
        }

        [HttpGet("bans")]
        [Authorize(Policy = "Permissions.BanManagementPage.Read")]
        public async Task<ActionResult<SharedLibraryCore.Helpers.ResourceQueryHelperResult<BanInfo>>> GetBans([FromQuery] BanInfoRequest request)
        {
            var results = await _banInfoQueryHelper.QueryResource(request);
            return Ok(results);
        }

        [HttpGet("alerts")]
        [Authorize]
        public ActionResult<IEnumerable<SharedLibraryCore.Alerts.Alert.AlertState>> GetAlerts()
        {
            return Ok(_alertManager.RetrieveAlerts(Client));
        }

        [HttpPost("alerts/{id:guid}/dismiss")]
        [Authorize]
        public ActionResult DismissAlert(Guid id)
        {
            _alertManager.MarkAlertAsRead(id);
            return Ok();
        }
        
        [HttpPost("alerts/dismiss/all")]
        [Authorize]
        public ActionResult DismissAllAlerts()
        {
            _alertManager.MarkAllAlertsAsRead(Client.ClientId);
            return Ok();
        }

        [HttpGet("reports")]
        [Authorize]
        public ActionResult<IEnumerable<WebfrontCore.Controllers.API.Dtos.ServerReportsDto>> GetReports()
        {
            var reports = Manager.GetServers()
                .Select(server => new WebfrontCore.Controllers.API.Dtos.ServerReportsDto
                {
                    Id = server.EndPoint,
                    Name = server.ServerName,
                    Reports = server.Reports.Select(r => new WebfrontCore.Controllers.API.Dtos.ReportDto
                    {
                        Target = new WebfrontCore.Controllers.API.Dtos.EntityDto { Name = r.Target.Name, ClientId = r.Target.ClientId },
                        Origin = new WebfrontCore.Controllers.API.Dtos.EntityDto { Name = r.Origin.Name, ClientId = r.Origin.ClientId },
                        Reason = r.Reason,
                        ReportedOn = r.ReportedOn
                    }).OrderByDescending(r => r.ReportedOn).ToList()
                })
                .Where(s => s.Reports.Any())
                .ToList();
            return Ok(reports);
        }
    }
}
