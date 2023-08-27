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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Data.Abstractions;
using Stats.Config;
using WebfrontCore.QueryHelpers.Models;

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
        private readonly IServerDataViewer _serverDataViewer;
        private readonly StatManager _statManager;

        public StatsController(ILogger<StatsController> logger, IManager manager, IResourceQueryHelper<ChatSearchQuery,
                MessageResponse> resourceQueryHelper, ITranslationLookup translationLookup,
            IDatabaseContextFactory contextFactory, StatsConfiguration config, IServerDataViewer serverDataViewer, StatManager statManager) : base(manager)
        {
            _logger = logger;
            _manager = manager;
            _chatResourceQueryHelper = resourceQueryHelper;
            _translationLookup = translationLookup;
            _contextFactory = contextFactory;
            _config = config;
            _serverDataViewer = serverDataViewer;
            _statManager = statManager;
        }

        [HttpGet]
        public async Task<IActionResult> TopPlayers(string serverId = null, CancellationToken token = default)
        {
            ViewBag.Title = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_STATS_INDEX_TITLE"];
            ViewBag.Description = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_STATS_INDEX_DESC"];
            ViewBag.Localization = _translationLookup;
            ViewBag.SelectedServerId = serverId;
            
            var server = _manager.GetServers().FirstOrDefault(server => server.Id == serverId) as IGameServer;
            long? matchedServerId = null;

            if (server != null)
            {
                matchedServerId = server.LegacyDatabaseId;
            }
            
            ViewBag.TotalRankedClients = await _serverDataViewer.RankedClientsCountAsync(matchedServerId, token);
            ViewBag.ServerId = matchedServerId;

            return View("~/Views/Client/Statistics/Index.cshtml", _manager.GetServers()
                .Select(selectedServer => new ServerInfo
                {
                    Name = selectedServer.Hostname,
                    IPAddress = selectedServer.ListenAddress,
                    Port = selectedServer.ListenPort,
                    Game = selectedServer.GameCode
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

            if (_manager.GetServers().FirstOrDefault(activeServer => activeServer.EndPoint == serverId) is IGameServer server)
            {
                serverId = server.LegacyDatabaseId;
            }

            var results = _config?.EnableAdvancedMetrics ?? true
                ? await _statManager.GetNewTopStats(offset, count, serverId)
                : await _statManager.GetTopStats(offset, count, serverId);

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
        public async Task<IActionResult> FindMessage([FromQuery] ChatResourceRequest query)
        {
            ViewBag.Localization = _translationLookup;
            ViewBag.EnableColorCodes = _manager.GetApplicationSettings().Configuration().EnableColorCodes;
            ViewBag.Query = query;
            ViewBag.QueryLimit = 100;
            ViewBag.Title = _translationLookup["WEBFRONT_STATS_MESSAGES_TITLE"];
            ViewBag.Error = null;
            ViewBag.IsFluid = true;
        
            var result = query != null ? await _chatResourceQueryHelper.QueryResource(query) : null;
            return View("~/Views/Client/Message/Find.cshtml", result);
        }

        [HttpGet("Message/FindNext")]
        public async Task<IActionResult> FindNextMessages(ChatResourceRequest query)
        {
            var result = await _chatResourceQueryHelper.QueryResource(query);
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
                .ThenInclude(angles => angles.Vector)
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
                    new()
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
