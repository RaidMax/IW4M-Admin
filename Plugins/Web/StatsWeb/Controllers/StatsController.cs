﻿using IW4MAdmin.Plugins.Stats;
using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using StatsWeb.Dtos;
using StatsWeb.Extensions;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IW4MAdmin.Plugins.Web.StatsWeb.Controllers
{
    public class StatsController : BaseController
    {
        private readonly ILogger _logger;
        private readonly IManager _manager;
        private readonly IResourceQueryHelper<ChatSearchQuery, ChatSearchResult> _chatResourceQueryHelper;
        private readonly ITranslationLookup _translationLookup;

        public StatsController(ILogger logger, IManager manager, IResourceQueryHelper<ChatSearchQuery, ChatSearchResult> resourceQueryHelper,
            ITranslationLookup translationLookup) : base(manager)
        {
            _logger = logger;
            _manager = manager;
            _chatResourceQueryHelper = resourceQueryHelper;
            _translationLookup = translationLookup;
        }

        [HttpGet]
        public IActionResult TopPlayersAsync()
        {
            ViewBag.Title = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_STATS_INDEX_TITLE"];
            ViewBag.Description = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_STATS_INDEX_DESC"];
            ViewBag.Servers = _manager.GetServers().Select(_server => new ServerInfo() { Name = _server.Hostname, ID = _server.EndPoint });
            ViewBag.Localization = _translationLookup;

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

            var server = _manager.GetServers().FirstOrDefault(_server => _server.EndPoint == serverId);

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
                var iqMessages = from message in ctx.Set<Stats.Models.EFClientMessage>()
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

                var messages = await iqMessages.ToListAsync();

                foreach (var message in messages)
                {
                    if (message.Message.IsQuickMessage())
                    {
                        try
                        {
                            var quickMessages = _manager.GetApplicationSettings().Configuration()
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

        [HttpGet("Message/Find")]
        public async Task<IActionResult> FindMessage([FromQuery]string query)
        {
            ViewBag.Localization = _translationLookup;
            ViewBag.EnableColorCodes = _manager.GetApplicationSettings().Configuration().EnableColorCodes;
            ViewBag.Query = query;
            ViewBag.QueryLimit = 100;
            ViewBag.Title = _translationLookup["WEBFRONT_STATS_MESSAGES_TITLE"];
            ViewBag.Error = null;
            ViewBag.IsFluid = true;
            ChatSearchQuery searchRequest = null;

            try
            {
                searchRequest = query.ParseSearchInfo(int.MaxValue, 0);
            }

            catch (ArgumentException e)
            {
                _logger.WriteWarning($"Could not parse chat message search query - {query}");
                _logger.WriteDebug(e.GetExceptionInfo());
                ViewBag.Error = e;
            }

            catch (FormatException e)
            {
                _logger.WriteWarning($"Could not parse chat message search query filter format - {query}");
                _logger.WriteDebug(e.GetExceptionInfo());
                ViewBag.Error = e;
            }

            var result = searchRequest != null ? await _chatResourceQueryHelper.QueryResource(searchRequest) : null;
            return View("Message/Find", result);
        }

        [HttpGet("Message/FindNext")]
        public async Task<IActionResult> FindNextMessages([FromQuery]string query, [FromQuery]int count, [FromQuery]int offset)
        {
            ChatSearchQuery searchRequest;

            try
            {
                searchRequest = query.ParseSearchInfo(count, offset);
            }

            catch (ArgumentException e)
            {
                _logger.WriteWarning($"Could not parse chat message search query - {query}");
                _logger.WriteDebug(e.GetExceptionInfo());
                throw;
            }

            catch (FormatException e)
            {
                _logger.WriteWarning($"Could not parse chat message search query filter format - {query}");
                _logger.WriteDebug(e.GetExceptionInfo());
                throw;
            }

            var result = await _chatResourceQueryHelper.QueryResource(searchRequest);
            return PartialView("Message/_Item", result.Results);
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

                if (penalty == null)
                {
                    return NotFound();
                }

                // todo: this can be optimized
                var iqSnapshotInfo = ctx.Set<Stats.Models.EFACSnapshot>()
                    .Where(s => s.ClientId == penalty.OffenderId)
                    .Include(s => s.LastStrainAngle)
                    .Include(s => s.HitOrigin)
                    .Include(s => s.HitDestination)
                    .Include(s => s.CurrentViewAngle)
                    .Include(s => s.PredictedViewAngles)
                    .ThenInclude(_angles => _angles.Vector)
                    .OrderBy(s => s.When)
                    .ThenBy(s => s.Hits);

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
