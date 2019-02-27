using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebfrontCore.Controllers;

namespace IW4MAdmin.Plugins.Stats.Web.Controllers
{
    public class StatsController : BaseController
    {
        [HttpGet]
        public IActionResult TopPlayersAsync()
        {
            ViewBag.Title = Utilities.CurrentLocalization.LocalizationIndex.Set["WEBFRONT_STATS_INDEX_TITLE"];
            ViewBag.Description = Utilities.CurrentLocalization.LocalizationIndex.Set["WEBFRONT_STATS_INDEX_DESC"];
            ViewBag.Servers = Manager.GetServers().Select(_server => new ServerInfo() { Name = _server.Hostname, ID = _server.GetHashCode() });

            return View("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetTopPlayersAsync(int count, int offset, long? serverId = null)
        {
            // this prevents empty results when we really want aggregate
            if (serverId == 0)
            {
                serverId = null;
            }

            var server = Manager.GetServers().FirstOrDefault(_server => _server.EndPoint == serverId);

            if (server != null)
            {
                serverId = await StatManager.GetIdForServer(server);
            }

            var results = await Plugin.Manager.GetTopStats(offset, count, serverId);

            // this returns an empty result so we know to stale the loader
            if (results.Count == 0 && offset > 0)
            {
                return Ok();
            }

            else
            {
                return View("Components/TopPlayers/_List", results);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMessageAsync(int serverId, DateTime when)
        {
            var whenUpper = when.AddMinutes(5);
            var whenLower = when.AddMinutes(-5);

            using (var ctx = new SharedLibraryCore.Database.DatabaseContext(true))
            {
                var iqMessages = from message in ctx.Set<Models.EFClientMessage>()
                                 where message.ServerId == serverId
                                 where message.TimeSent >= whenLower
                                 where message.TimeSent <= whenUpper
                                 select new ChatInfo()
                                 {
                                     ClientId = message.ClientId,
                                     Message = message.Message,
                                     Name = message.Client.CurrentAlias.Name,
                                     Time = message.TimeSent
                                 };

#if DEBUG == true
                var messagesSql = iqMessages.ToSql();
#endif

                var messages = await iqMessages.ToListAsync();

                return View("_MessageContext", messages);
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAutomatedPenaltyInfoAsync(int clientId)
        {
            using (var ctx = new SharedLibraryCore.Database.DatabaseContext(true))
            {
                var penaltyInfo = await ctx.Set<Models.EFACSnapshot>()
                    .Where(s => s.ClientId == clientId)
                    .Include(s => s.LastStrainAngle)
                    .Include(s => s.HitOrigin)
                    .Include(s => s.HitDestination)
                    .Include(s => s.CurrentViewAngle)
                    .Include(s => s.PredictedViewAngles)
                    .OrderBy(s => s.When)
                    .ThenBy(s => s.Hits)
                    .ToListAsync();

                return View("_PenaltyInfo", penaltyInfo);
            }
        }
    }
}
