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
            ViewBag.Title = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_STATS_INDEX_TITLE"];
            ViewBag.Description = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_STATS_INDEX_DESC"];
            ViewBag.Servers = Manager.GetServers().Select(_server => new ServerInfo() { Name = _server.Hostname, ID = _server.EndPoint });

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
                serverId = StatManager.GetIdForServer(server);
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
        public async Task<IActionResult> GetMessageAsync(int serverId, long when)
        {
            var whenTime = DateTime.FromFileTimeUtc(when);
            var whenUpper = whenTime.AddMinutes(5);
            var whenLower = whenTime.AddMinutes(-5);

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
                                     Time = message.TimeSent,
                                     ServerGame = message.Server.GameName ?? Server.Game.IW4
                                 };

#if DEBUG == true
                var messagesSql = iqMessages.ToSql();
#endif
                var messages = await iqMessages.ToListAsync();

                foreach (var message in messages)
                {
                    if (message.Message.IsQuickMessage())
                    {
                        try
                        {
                            var quickMessages = Manager.GetApplicationSettings().Configuration()
                                .QuickMessages
                                .First(_qm => _qm.Game == message.ServerGame);
                            message.Message = quickMessages.Messages[message.Message.Substring(1)];
                            message.IsQuickMessage = true;
                        }
                        catch { }
                    }
                }

                return View("_MessageContext", messages);
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAutomatedPenaltyInfoAsync(int penaltyId)
        {
            using (var ctx = new SharedLibraryCore.Database.DatabaseContext(true))
            {
                var penalty = await ctx.Penalties
                    .Select(_penalty => new { _penalty.OffenderId, _penalty.PenaltyId, _penalty.When, _penalty.AutomatedOffense })
                    .FirstOrDefaultAsync(_penalty => _penalty.PenaltyId == penaltyId);

                var iqSnapshotInfo = ctx.Set<Models.EFACSnapshot>()
                    .Where(s => s.ClientId == penalty.OffenderId)
                    .Include(s => s.LastStrainAngle)
                    .Include(s => s.HitOrigin)
                    .Include(s => s.HitDestination)
                    .Include(s => s.CurrentViewAngle)
                    .Include(s => s.PredictedViewAngles)
                    .OrderBy(s => s.When)
                    .ThenBy(s => s.Hits);

#if DEBUG == true
                var sql = iqSnapshotInfo.ToSql();
#endif
                var penaltyInfo = await iqSnapshotInfo.ToListAsync();

                if (penaltyInfo.Count > 0)
                {
                    return View("_PenaltyInfo", penaltyInfo);
                }

                // we want to show anything related to the automated offense 
                else
                {
                    return View("_MessageContext", new[]
                    {
                        new ChatInfo()
                        {
                            ClientId = penalty.OffenderId,
                            Message = penalty.AutomatedOffense,
                            Time = penalty.When
                        }
                    });
                }
            }
        }
    }
}
