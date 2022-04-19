using LiveRadar.Configuration;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace LiveRadar.Web.Controllers
{
    public class RadarController : BaseController
    {
        private readonly IManager _manager;
        private static LiveRadarConfiguration _config;
        private readonly IConfigurationHandler<LiveRadarConfiguration> _configurationHandler;

        public RadarController(IManager manager, IConfigurationHandlerFactory configurationHandlerFactory) : base(manager)
        {
            _manager = manager;
            _configurationHandler =
                configurationHandlerFactory.GetConfigurationHandler<LiveRadarConfiguration>("LiveRadarConfiguration");
        }

        [HttpGet]
        [Route("Radar/{serverId}")]
        public IActionResult Index(string serverId = null)
        {
            var servers =  _manager.GetServers()
                .Where(server => server.GameName == Server.Game.IW4)
                .Select(server => new ServerInfo
                {
                    Name = server.Hostname,
                    IPAddress = server.IP,
                    Port = server.Port
                });
            
            ViewBag.Title = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_RADAR_TITLE"];
            ViewBag.SelectedServerId = string.IsNullOrEmpty(serverId) ? servers.FirstOrDefault()?.Endpoint : serverId;

            // ReSharper disable once Mvc.ViewNotResolved
            return View("~/Views/Plugins/LiveRadar/Radar/Index.cshtml", servers);
        }

        [HttpGet]
        [Route("Radar/{serverId}/Map")]
        public async Task<IActionResult> Map(long? serverId = null)
        {
            var server = serverId == null ? _manager.GetServers().FirstOrDefault() : _manager.GetServers().FirstOrDefault(_server => _server.EndPoint == serverId);
            
            if (server == null)
            {
                return NotFound();
            }

            if (_config == null)
            {
                await _configurationHandler.BuildAsync();
                _config = _configurationHandler.Configuration() ?? new LiveRadarConfiguration();
            }
            
            var map = _config.Maps.FirstOrDefault(_map => _map.Name == server.CurrentMap.Name);

            if (map == null)
            {
                // occurs if we don't recognize the map
                return StatusCode(StatusCodes.Status422UnprocessableEntity);
            }
            
            map.Alias = server.CurrentMap.Alias;
            return Json(map);
        }

        [HttpGet]
        [Route("Radar/{serverId}/Data")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Data(long? serverId = null)
        {
            var server = serverId == null ? _manager.GetServers()[0] : _manager.GetServers().First(_server => _server.EndPoint == serverId);
            var radarInfo = server.GetClientsAsList().Select(_client => _client.GetAdditionalProperty<RadarEvent>("LiveRadar")).ToList();
            return Json(radarInfo);
        }

        [HttpGet]
        [Route("Radar/Update")]
        public IActionResult Update(string payload)
        {
            /*var radarUpdate = RadarEvent.Parse(payload);
            var client = _manager.GetActiveClients().FirstOrDefault(_client => _client.NetworkId == radarUpdate.Guid);

            if (client != null)
            {
                radarUpdate.Name = client.Name.StripColors();
                client.SetAdditionalProperty("LiveRadar", radarUpdate);
            }*/

            return Ok();
        }
    }
}
