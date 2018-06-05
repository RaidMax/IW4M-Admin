using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebfrontCore.Controllers;

namespace IW4MAdmin.Plugins.Stats.Web.Controllers
{
    public class StatsController : BaseController
    {
        [HttpGet]
        public async Task<IActionResult> TopPlayersAsync()
        {
            ViewBag.Title = Utilities.CurrentLocalization.LocalizationIndex.Set["WEBFRONT_STATS_INDEX_TITLE"];
            ViewBag.Description = Utilities.CurrentLocalization.LocalizationIndex.Set["WEBFRONT_STATS_INDEX_DESC"];

            return View("Index", await Plugin.Manager.GetTopStats(0, 10));
        }

        [HttpGet]
        public async Task<IActionResult> GetTopPlayersAsync(int count, int offset)
        {
            return View("_List", await Plugin.Manager.GetTopStats(offset, count));
        }

        [HttpGet]
        public async Task<IActionResult> GetMessageAsync(int serverId, DateTime when)
        {
            var whenUpper = when.AddMinutes(5);
            var whenLower = when.AddMinutes(-5);

            using (var ctx = new SharedLibraryCore.Database.DatabaseContext())
            {
                var iqMessages = from message in ctx.Set<Models.EFClientMessage>()
                                 where message.ServerId == serverId
                                 where message.TimeSent >= whenLower
                                 where message.TimeSent <= whenUpper
                                 select new SharedLibraryCore.Dtos.ChatInfo()
                                 {
                                     Message = message.Message,
                                     Name = message.Client.CurrentAlias.Name,
                                     Time = message.TimeSent
                                 };

                var messages = await iqMessages.ToListAsync();

                return View("_MessageContext", messages);
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAutomatedPenaltyInfoAsync(int clientId)
        {
            using (var ctx = new SharedLibraryCore.Database.DatabaseContext())
            {
                var penaltyInfo = await ctx.Set<Models.EFACSnapshot>()
                    .Where(s => s.ClientId == clientId)
                    .Include(s => s.LastStrainAngle)
                    .Include(s => s.HitOrigin)
                    .Include(s => s.HitDestination)
                    .Include(s => s.CurrentViewAngle)
                    .Include(s => s.PredictedViewAngles)
                    .OrderBy(s => s.When)
                    .ToListAsync();

                if (penaltyInfo != null)
                {
                    return View("_PenaltyInfo", penaltyInfo);
                }

                return NotFound();
            }
        }
    }
}
