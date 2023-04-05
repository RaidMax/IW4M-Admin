using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using Stats.Dtos;

namespace WebfrontCore.Controllers
{
    [Route("clientstatistics")]
    public class ClientStatisticsController : BaseController
    {
        private IResourceQueryHelper<StatsInfoRequest, AdvancedStatsInfo> _queryHelper;
        private readonly DefaultSettings _defaultConfig;
        private readonly IServerDataViewer _serverDataViewer;

        public ClientStatisticsController(IManager manager,
            IResourceQueryHelper<StatsInfoRequest, AdvancedStatsInfo> queryHelper,
            DefaultSettings defaultConfig, IServerDataViewer serverDataViewer) : base(manager)
        {
            _queryHelper = queryHelper;
            _defaultConfig = defaultConfig;
            _serverDataViewer = serverDataViewer;
        }

        [HttpGet("{id:int}/advanced")]
        public async Task<IActionResult> Advanced(int id, [FromQuery] string serverId, CancellationToken token = default)
        {
            ViewBag.Config = _defaultConfig.GameStrings;
            var hitInfo = (await _queryHelper.QueryResource(new StatsInfoRequest
            {
                ClientId = id,
                ServerEndpoint = serverId
            }))?.Results?.First();

            if (hitInfo is null)
            {
                return NotFound();
            }
            
            var server = Manager.GetServers().FirstOrDefault(server => server.Id == serverId) as IGameServer;
            long? matchedServerId = null;

            if (server != null)
            {
                matchedServerId = server.LegacyDatabaseId;
            }

            hitInfo.TotalRankedClients = await _serverDataViewer.RankedClientsCountAsync(matchedServerId, token);

            return View("~/Views/Client/Statistics/Advanced.cshtml", hitInfo);
        }
    }
}
