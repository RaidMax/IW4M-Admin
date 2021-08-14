using LiveRadar.Configuration;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace LiveRadar.Web.Controllers
{
    public class RadarController : BaseController
    {
        private static readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
        };

        private readonly IManager _manager;
        private readonly LiveRadarConfiguration _config;

        public RadarController(IManager manager, IConfigurationHandlerFactory configurationHandlerFactory) : base(manager)
        {
            _manager = manager;
            _config = configurationHandlerFactory.GetConfigurationHandler<LiveRadarConfiguration>("LiveRadarConfiguration").Configuration() ?? new LiveRadarConfiguration();
        }

        [HttpGet]
        [Route("Radar/{serverId}")]
        public IActionResult Index(long? serverId = null)
        {
            ViewBag.IsFluid = true;
            ViewBag.Title = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_RADAR_TITLE"];
            ViewBag.ActiveServerId = serverId ?? _manager.GetServers().FirstOrDefault()?.EndPoint;
            ViewBag.Servers = _manager.GetServers()
                .Where(_server => _server.GameName == Server.Game.IW4)
                .Select(_server => new ServerInfo()
                {
                    Name = _server.Hostname,
                    ID = _server.EndPoint
                });

            return View();
        }

        [HttpGet]
        [Route("Radar/{serverId}/Map")]
        public IActionResult Map(long? serverId = null)
        {
            var server = serverId == null ? _manager.GetServers().FirstOrDefault() : _manager.GetServers().FirstOrDefault(_server => _server.EndPoint == serverId);
            
            if (server == null)
            {
                return NotFound();
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