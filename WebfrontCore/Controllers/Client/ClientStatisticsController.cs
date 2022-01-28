using System.Linq;
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

        public ClientStatisticsController(IManager manager,
            IResourceQueryHelper<StatsInfoRequest, AdvancedStatsInfo> queryHelper,
            DefaultSettings defaultConfig) : base(manager)
        {
            _queryHelper = queryHelper;
            _defaultConfig = defaultConfig;
        }

        [HttpGet("{id:int}/advanced")]
        public async Task<IActionResult> Advanced(int id, [FromQuery] string serverId)
        {
            ViewBag.Config = _defaultConfig.GameStrings;
            var hitInfo = await _queryHelper.QueryResource(new StatsInfoRequest
            {
                ClientId = id,
                ServerEndpoint = serverId
            });

            return View("~/Views/Client/Statistics/Advanced.cshtml", hitInfo.Results.First());
        }
    }
}
