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

        public async Task<IActionResult> ListAsync(int offset = 0, int count = 30, EFPenalty.PenaltyType showOnly = EFPenalty.PenaltyType.Any, bool hideAutomatedPenalties = true)
        {
            return await Task.FromResult(View("_List", new ViewModels.PenaltyFilterInfo
            {
                Offset = offset,
                Count = count,
                ShowOnly = showOnly,
                IgnoreAutomated = hideAutomatedPenalties
            }));
        }
    }
}
