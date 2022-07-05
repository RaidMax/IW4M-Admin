using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Threading.Tasks;
using WebfrontCore.QueryHelpers.Models;

namespace WebfrontCore.Controllers
{
    public class AdminController : BaseController
    {
        private readonly IAuditInformationRepository _auditInformationRepository;
        private readonly ITranslationLookup _translationLookup;
        private readonly IResourceQueryHelper<BanInfoRequest, BanInfo> _banInfoQueryHelper;
        private static readonly int DEFAULT_COUNT = 25;

        public AdminController(IManager manager, IAuditInformationRepository auditInformationRepository,
            ITranslationLookup translationLookup,
            IResourceQueryHelper<BanInfoRequest, BanInfo> banInfoQueryHelper) : base(manager)
        {
            _auditInformationRepository = auditInformationRepository;
            _translationLookup = translationLookup;
            _banInfoQueryHelper = banInfoQueryHelper;
        }

        [Authorize]
        public async Task<IActionResult> AuditLog()
        {
            ViewBag.EnableColorCodes = Manager.GetApplicationSettings().Configuration().EnableColorCodes;
            ViewBag.IsFluid = true;
            ViewBag.Title = _translationLookup["WEBFRONT_NAV_AUDIT_LOG"];
            ViewBag.InitialOffset = DEFAULT_COUNT;

            var auditItems = await _auditInformationRepository.ListAuditInformation(new PaginationRequest
            {
                Count = DEFAULT_COUNT
            });

            return View(auditItems);
        }

        public async Task<IActionResult> ListAuditLog([FromQuery] PaginationRequest paginationInfo)
        {
            ViewBag.EnableColorCodes = Manager.GetApplicationSettings().Configuration().EnableColorCodes;
            var auditItems = await _auditInformationRepository.ListAuditInformation(paginationInfo);
            return PartialView("_ListAuditLog", auditItems);
        }

        public async Task<IActionResult> BanManagement([FromQuery] BanInfoRequest request)
        {
            var results = await _banInfoQueryHelper.QueryResource(request);

            ViewBag.ClientName = request.ClientName;
            ViewBag.ClientId = request.ClientId;
            ViewBag.ClientIP = request.ClientIP;
            ViewBag.ClientGuid = request.ClientGuid;
            
            ViewBag.Title = Localization["WEBFRONT_NAV_TITLE_BAN_MANAGEMENT"];

            return View(results.Results);
        }

        public async Task<IActionResult> BanManagementList([FromQuery] BanInfoRequest request)
        {
            var results = await _banInfoQueryHelper.QueryResource(request);
            return PartialView("_BanEntries", results.Results);
        }
    }
}
