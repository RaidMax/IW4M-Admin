using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using Stats.Config;
using Stats.Dtos;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/stats")]
    public class StatsController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly IResourceQueryHelper<StatsInfoRequest, StatsInfoResult> _statsQueryHelper;
        private readonly IResourceQueryHelper<StatsInfoRequest, AdvancedStatsInfo> _advancedStatsQueryHelper;
        private readonly StatManager _statManager;
        private readonly StatsConfiguration _config;
        private readonly IManager _manager;
        private readonly SharedLibraryCore.Interfaces.IServerDataViewer _serverDataViewer;

        public StatsController(ILogger<StatsController> logger, IResourceQueryHelper<StatsInfoRequest, StatsInfoResult> statsQueryHelper,
            StatManager statManager, StatsConfiguration config, IManager manager, SharedLibraryCore.Interfaces.IServerDataViewer serverDataViewer,
            IResourceQueryHelper<StatsInfoRequest, AdvancedStatsInfo> advancedStatsQueryHelper, Data.Abstractions.IDatabaseContextFactory contextFactory,
            IResourceQueryHelper<ChatSearchQuery, SharedLibraryCore.Dtos.Meta.Responses.MessageResponse> chatQueryHelper)
        {
            _statsQueryHelper = statsQueryHelper;
            _logger = logger;
            _statManager = statManager;
            _config = config;
            _manager = manager;
            _serverDataViewer = serverDataViewer;
            _advancedStatsQueryHelper = advancedStatsQueryHelper;
            _contextFactory = contextFactory; 
            _chatQueryHelper = chatQueryHelper;
        }

        private readonly Data.Abstractions.IDatabaseContextFactory _contextFactory;
        private readonly IResourceQueryHelper<ChatSearchQuery, SharedLibraryCore.Dtos.Meta.Responses.MessageResponse> _chatQueryHelper;

        [HttpGet("{clientId:int}/advanced")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAdvancedStats(int clientId, [FromQuery] string? serverId, CancellationToken token = default)
        {
            var hitInfo = (await _advancedStatsQueryHelper.QueryResource(new StatsInfoRequest
            {
                ClientId = clientId,
                ServerEndpoint = serverId
            }))?.Results?.First();

            if (hitInfo is null)
            {
                return NotFound();
            }
            
            var server = _manager.GetServers().FirstOrDefault(s => s.Id == serverId) as IGameServer;
            long? matchedServerId = server?.LegacyDatabaseId;

            hitInfo.TotalRankedClients = await _serverDataViewer.RankedClientsCountAsync(matchedServerId, token);
            return Ok(hitInfo);
        }

        [HttpGet("top")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTopPlayers([FromQuery] int count = 25, [FromQuery] int offset = 0, [FromQuery] string? serverId = null)
        {
             var server = _manager.GetServers().FirstOrDefault(s => s.Id == serverId) as IGameServer;
             var legacyId = server?.LegacyDatabaseId;

             var stats = _config?.EnableAdvancedMetrics ?? true
                   ? await _statManager.GetNewTopStats(offset, count, legacyId)
                   : await _statManager.GetTopStats(offset, count, legacyId);

             var totalRanked = await _serverDataViewer.RankedClientsCountAsync(legacyId);

             return Ok(new WebfrontCore.Controllers.API.Dtos.TopStatsResponse
             {
                 Players = stats,
                 TotalRankedClients = totalRanked
             });
        }

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("{clientId}")]
        public async Task<IActionResult> ClientStats(int clientId)
        {
            if (clientId < 1 || !ModelState.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    Messages = new[] { $"Client Id must be between 1 and {int.MaxValue}" }
                });

            }

            var request = new StatsInfoRequest()
            {
                ClientId = clientId
            };

            try
            {
                var result = await _statsQueryHelper.QueryResource(request);

                if (result.RetrievedResultCount == 0)
                {
                    return NotFound();
                }

                return Ok(result.Results);
            }

            catch (Exception e)
            {
                _logger.LogWarning(e, "Could not get client stats for client id {clientId}", clientId);

                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
                {
                    Messages = new[] { e.Message }
                });
            }
        }

        [HttpGet("message/context")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMessageContext([FromQuery] string serverId, [FromQuery] long when)
        {
            var whenTime = DateTime.FromFileTimeUtc(when);
            var whenUpper = whenTime.AddMinutes(5);
            var whenLower = whenTime.AddMinutes(-5);

            var messages = await _chatQueryHelper.QueryResource(new ChatSearchQuery
            {
                ServerId = serverId,
                SentBefore = whenUpper,
                SentAfter = whenLower
            });

            return Ok(messages.Results.OrderBy(message => message.When));
        }

        [HttpGet("penalty/{penaltyId}/context")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(Policy = "Permissions.BanManagementPage.Read")]
        public async Task<IActionResult> GetAutomatedPenaltyInfo(int penaltyId)
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
                var formattedInfo = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>();
                
                foreach (var snapshot in penaltyInfo)
                {
                    var snapshotDict = new System.Collections.Generic.Dictionary<string, string>();
                    var props = snapshot.GetType().GetProperties().OrderBy(prop => prop.Name);

                    foreach (var prop in props)
                    {
                         if ((prop.Name.EndsWith("Id") && prop.Name != "WeaponId" || prop.Name == "Server") || 
                             new[] {"Active", "Client", "PredictedViewAngles"}.Contains(prop.Name))
                        {
                            continue;
                        }
                        
                        var value = prop.GetValue(snapshot)?.ToString()?.StripColors();
                        snapshotDict.Add(prop.Name, value);
                    }
                    formattedInfo.Add(snapshotDict);
                }

                return Ok(formattedInfo);
            }
            else
            {
                // Fallback to message context logic if no snapshots
                 return Ok(new System.Collections.Generic.List<SharedLibraryCore.Dtos.Meta.Responses.MessageResponse>
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
