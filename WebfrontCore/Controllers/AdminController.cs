using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Threading.Tasks;

namespace WebfrontCore.Controllers
{
    public class AdminController : BaseController
    {
        private readonly IAuditInformationRepository _auditInformationRepository;
        private readonly ITranslationLookup _translationLookup;
        private static readonly int DEFAULT_COUNT = 25;

        public AdminController(IManager manager, IAuditInformationRepository auditInformationRepository, ITranslationLookup translationLookup) : base(manager)
        {
            _auditInformationRepository = auditInformationRepository;
            _translationLookup = translationLookup;
        }

        [Authorize]
        public async Task<IActionResult> AuditLog()
        {
            ViewBag.EnableColorCodes = Manager.GetApplicationSettings().Configuration().EnableColorCodes;
            ViewBag.IsFluid = true;
            ViewBag.Title = _translationLookup["WEBFRONT_NAV_AUDIT_LOG"];
            ViewBag.InitialOffset = DEFAULT_COUNT;

            var auditItems = await _auditInformationRepository.ListAuditInformation(new PaginationInfo()
            {
                Count = DEFAULT_COUNT
            });

            return View(auditItems);
        }

        public async Task<IActionResult> ListAuditLog([FromQuery] PaginationInfo paginationInfo)
        {
            ViewBag.EnableColorCodes = Manager.GetApplicationSettings().Configuration().EnableColorCodes;
            var auditItems = await _auditInformationRepository.ListAuditInformation(paginationInfo);
            return PartialView("_ListAuditLog", auditItems);
        }
    }
}
