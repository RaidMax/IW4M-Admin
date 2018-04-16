using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Objects;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.ViewComponents
{
    public class PenaltyListViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(int offset)
        {
            var penalties = await Program.Manager.GetPenaltyService().GetRecentPenalties(12, offset);
            var penaltiesDto = penalties.Select(p => new PenaltyInfo()
            {
                OffenderId = p.OffenderId,
                OffenderName = p.Offender.Name,
                PunisherId = p.PunisherId,
                PunisherName = p.Punisher.Name,
                PunisherLevel = p.Punisher.Level.ToString(),
                Offense = p.Offense,
                Type = p.Type.ToString(),
                TimePunished = Utilities.GetTimePassed(p.When, false),
                TimeRemaining = DateTime.UtcNow > p.Expires ? "" : Utilities.TimeSpanText(p.Expires - DateTime.UtcNow),
                Sensitive = p.Type == Penalty.PenaltyType.Flag
            });

            penaltiesDto = User.Identity.IsAuthenticated ? penaltiesDto.ToList() : penaltiesDto.Where(p => !p.Sensitive).ToList();

            return View("_List", penaltiesDto);
        }
    }
}
