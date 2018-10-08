using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebfrontCore.ViewComponents;

namespace WebfrontCore.Controllers
{
    public class PenaltyController : BaseController
    {
        public IActionResult List(int showOnly = (int)SharedLibraryCore.Objects.Penalty.PenaltyType.Any)
        {
            ViewBag.Description = "List of all the recent penalties (bans, kicks, warnings) on IW4MAdmin";
            ViewBag.Title = Localization["WEBFRONT_PENALTY_TITLE"];
            ViewBag.Keywords = "IW4MAdmin, penalties, ban, kick, warns";

            return View((SharedLibraryCore.Objects.Penalty.PenaltyType)showOnly);
        }

        public async Task<IActionResult> ListAsync(int offset = 0, int showOnly = (int)SharedLibraryCore.Objects.Penalty.PenaltyType.Any)
        {
            return await Task.FromResult(View("_List", new ViewModels.PenaltyFilterInfo()
            {
                Offset = offset,
                ShowOnly = (SharedLibraryCore.Objects.Penalty.PenaltyType)showOnly
            }));
        }

        public async Task<IActionResult> PublicAsync()
        {
            IList<EFPenalty> penalties;

            using (var ctx = new DatabaseContext(disableTracking: true))
            {
                penalties = await ctx.Penalties
                    .Where(p => p.Type == SharedLibraryCore.Objects.Penalty.PenaltyType.Ban && p.Active)
                    .ToListAsync();
            }

            var penaltiesDto = penalties.Select(p => new PenaltyInfo()
            {
                Id = p.PenaltyId,
                OffenderId = p.OffenderId,
                Offense = p.Offense,
                PunisherId = p.PunisherId,
                Type = p.Type.ToString(),
                TimePunished = p.When.ToString(),
                TimeRemaining = p.Expires.ToString(),
                AutomatedOffense = p.AutomatedOffense
            }).ToList();

            return Json(penaltiesDto);
        }
    }
}
