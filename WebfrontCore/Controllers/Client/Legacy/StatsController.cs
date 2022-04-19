using IW4MAdmin.Plugins.Stats;
using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using Stats.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Data.Abstractions;
using IW4MAdmin.Plugins.Stats.Config;
using Stats.Config;

namespace IW4MAdmin.Plugins.Web.StatsWeb.Controllers
{
    public class StatsController : BaseController
    {
        private readonly ILogger _logger;
        private readonly IManager _manager;
        private readonly IResourceQueryHelper<ChatSearchQuery, MessageResponse> _chatResourceQueryHelper;
        private readonly ITranslationLookup _translationLookup;
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly StatsConfiguration _config;

        public StatsController(ILogger<StatsController> logger, IManager manager, IResourceQueryHelper<ChatSearchQuery,
                MessageResponse> resourceQueryHelper, ITranslationLookup translationLookup,
            IDatabaseContextFactory contextFactory, StatsConfiguration config) : base(manager)
        {
            _logger = logger;
            _manager = manager;
            _chatResourceQueryHelper = resourceQueryHelper;
            _translationLookup = translationLookup;
            _contextFactory = contextFactory;
            _config = config;
        }

        [HttpGet]
        public IActionResult TopPlayers(string serverId = null)
        {
            ViewBag.Title = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_STATS_INDEX_TITLE"];
            ViewBag.Description = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_STATS_INDEX_DESC"];
            ViewBag.Localization = _translationLookup;
            ViewBag.SelectedServerId = serverId;

            return View("~/Views/Client/Statistics/Index.cshtml", _manager.GetServers()
                .Select(server => new ServerInfo
                {
                    Name = server.Hostname,
                    IPAddress = server.IP,
                    Port = server.Port
                }));
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

            var results = _config?.EnableAdvancedMetrics ?? true
                ? await Plugin.Manager.GetNewTopStats(offset, count, serverId)
                : await Plugin.Manager.GetTopStats(offset, count, serverId);

            // this returns an empty result so we know to stale the loader
            if (results.Count == 0 && offset > 0)
            {
                return Ok();
            }

            ViewBag.UseNewStats = _config?.EnableAdvancedMetrics;
            return View("~/Views/Client/Statistics/Components/TopPlayers/_List.cshtml", results);
        }

        [HttpGet]
        public async Task<IActionResult> GetMessageAsync(string serverId, long when)
        {
            var whenTime = DateTime.FromFileTimeUtc(when);
            var whenUpper = whenTime.AddMinutes(5);
            var whenLower = whenTime.AddMinutes(-5);

            var messages = await _chatResourceQueryHelper.QueryResource(new ChatSearchQuery
            {
                ServerId = serverId,
                SentBefore = whenUpper,
                SentAfter = whenLower
            });

            return View("~/Views/Client/_MessageContext.cshtml",
                messages.Results.OrderBy(message => message.When).ToList());
        }

        [HttpGet("Message/Find")]
        public async Task<IActionResult> FindMessage([FromQuery] string query)
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
                _logger.LogWarning(e, "Could not parse chat message search query {query}", query);
                ViewBag.Error = e;
            }

            catch (FormatException e)
            {
                _logger.LogWarning(e, "Could not parse chat message search query filter format {query}", query);
                ViewBag.Error = e;
            }

            var result = searchRequest != null ? await _chatResourceQueryHelper.QueryResource(searchRequest) : null;
            return View("~/Views/Client/Message/Find.cshtml", result);
        }

        [HttpGet("Message/FindNext")]
        public async Task<IActionResult> FindNextMessages([FromQuery] string query, [FromQuery] int count,
            [FromQuery] int offset)
        {
            ChatSearchQuery searchRequest;

            try
            {
                searchRequest = query.ParseSearchInfo(count, offset);
            }

            catch (ArgumentException e)
            {
                _logger.LogWarning(e, "Could not parse chat message search query {query}", query);
                throw;
            }

            catch (FormatException e)
            {
                _logger.LogWarning(e, "Could not parse chat message search query filter format {query}", query);
                throw;
            }

            var result = await _chatResourceQueryHelper.QueryResource(searchRequest);
            return PartialView("~/Views/Client/Message/_Item.cshtml", result.Results);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAutomatedPenaltyInfoAsync(int penaltyId)
        {
            await using var context = _contextFactory.CreateContext(false);

            var penalty = await context.Penalties
                .Select(_penalty => new
                    { _penalty.OffenderId, _penalty.PenaltyId, _penalty.When, _penalty.AutomatedOffense })
                .FirstOrDefaultAsync(_penalty => _penalty.PenaltyId == penaltyId);

            if (penalty == null)
            {
                return NotFound();
            }

            // todo: this can be optimized
            var iqSnapshotInfo = context.ACSnapshots
                .Where(s => s.ClientId == penalty.OffenderId)
                .Include(s => s.LastStrainAngle)
                .Include(s => s.HitOrigin)
                .Include(s => s.HitDestination)
                .Include(s => s.CurrentViewAngle)
                .Include(s => s.Server)
                .Include(s => s.PredictedViewAngles)
                .ThenInclude(_angles => _angles.Vector)
                .OrderBy(s => s.When)
                .ThenBy(s => s.Hits);

            var penaltyInfo = await iqSnapshotInfo.ToListAsync();

            if (penaltyInfo.Count > 0)
            {
                return View("~/Views/Client/_PenaltyInfo.cshtml", penaltyInfo);
            }

            // we want to show anything related to the automated offense 
            else
            {
                return View("~/Views/Client/_MessageContext.cshtml", new List<MessageResponse>
                {
                    new MessageResponse()
                    {
                        ClientId = penalty.OffenderId,
                        Message = penalty.AutomatedOffense,
                        When = penalty.When
                    }
                });
            }
        }
    }
}
