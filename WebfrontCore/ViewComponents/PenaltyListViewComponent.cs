using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.ViewComponents
{
    public class PenaltyListViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(int offset)
        {
            int ip = HttpContext.Connection.RemoteIpAddress
                .ToString().ConvertToIP();

            bool authed = false;

            try
            {
               // var a = IW4MAdmin.ApplicationManager.GetInstance()
               //.PrivilegedClients[HttpContext.Connection.RemoteIpAddress.ToString().ConvertToIP()];
            }

            catch (KeyNotFoundException)
            {

            }

            var penalties = await Program.Manager.GetPenaltyService().GetRecentPenalties(15, offset);
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
                Sensitive = p.Type == SharedLibraryCore.Objects.Penalty.PenaltyType.Flag
            });

            penaltiesDto = authed ? penaltiesDto.ToList() : penaltiesDto.Where(p => !p.Sensitive).ToList();

            return View("_List", penaltiesDto);
        }
    }
}
