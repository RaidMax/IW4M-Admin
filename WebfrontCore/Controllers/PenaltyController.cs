using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;

namespace WebfrontCore.Controllers
{
    public class PenaltyController : BaseController
    {
        private readonly IDatabaseContextFactory _contextFactory;
        
        public PenaltyController(IManager manager, IDatabaseContextFactory contextFactory) : base(manager)
        {
            _contextFactory = contextFactory;
        }

        public IActionResult List(EFPenalty.PenaltyType showOnly = EFPenalty.PenaltyType.Any, bool hideAutomatedPenalties = true)
        {
            ViewBag.Description = Localization["WEBFRONT_DESCRIPTION_PENALTIES"];
            ViewBag.Title = Localization["WEBFRONT_PENALTY_TITLE"];
            ViewBag.Keywords = Localization["WEBFRONT_KEYWORDS_PENALTIES"];
            ViewBag.HideAutomatedPenalties = hideAutomatedPenalties;

            return View(showOnly);
        }

        public async Task<IActionResult> ListAsync(int offset = 0, EFPenalty.PenaltyType showOnly = EFPenalty.PenaltyType.Any, bool hideAutomatedPenalties = true)
        {
            return await Task.FromResult(View("_List", new ViewModels.PenaltyFilterInfo()
            {
                Offset = offset,
                ShowOnly = showOnly,
                IgnoreAutomated = hideAutomatedPenalties
            }));
        }

        /// <summary>
        /// retrieves all permanent bans ordered by ban date
        /// if request is authorized, it will include the client's ip address.
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> PublicAsync()
        {
            IList<PenaltyInfo> penalties;

            await using var ctx = _contextFactory.CreateContext(false);
            var iqPenalties = ctx.Penalties
                .AsNoTracking()
                .Where(p => p.Type == EFPenalty.PenaltyType.Ban && p.Active)
                .OrderByDescending(_penalty => _penalty.When)
                .Select(p => new PenaltyInfo()
                {
                    Id = p.PenaltyId,
                    OffenderId = p.OffenderId,
                    OffenderName = p.Offender.CurrentAlias.Name,
                    OffenderNetworkId = (ulong)p.Offender.NetworkId,
                    OffenderIPAddress = Authorized ? p.Offender.CurrentAlias.IPAddress.ConvertIPtoString() : null,
                    Offense = p.Offense,
                    PunisherId = p.PunisherId,
                    PunisherNetworkId = (ulong)p.Punisher.NetworkId,
                    PunisherName = p.Punisher.CurrentAlias.Name,
                    PunisherIPAddress = Authorized ? p.Punisher.CurrentAlias.IPAddress.ConvertIPtoString() : null,
                    TimePunished = p.When,
                    AutomatedOffense = Authorized ? p.AutomatedOffense : null,
                });

            penalties = await iqPenalties.ToListAsync();

            return Json(penalties);
        }
    }
}
