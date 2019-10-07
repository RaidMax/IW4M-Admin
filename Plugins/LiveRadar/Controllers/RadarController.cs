using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using System.Linq;
using WebfrontCore.Controllers;

namespace LiveRadar.Web.Controllers
{
    public class RadarController : BaseController
    {
        private static readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
        };

        [HttpGet]
        [Route("Radar/{serverId}")]
        public IActionResult Index([FromQuery] long? serverId = null)
        {
            ViewBag.IsFluid = true;
            ViewBag.Title = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_RADAR_TITLE"];
            ViewBag.ActiveServerId = serverId ?? Manager.GetServers().FirstOrDefault()?.EndPoint;
            ViewBag.Servers = Manager.GetServers()
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
            var server = serverId == null ? Manager.GetServers().FirstOrDefault() : Manager.GetServers().FirstOrDefault(_server => _server.EndPoint == serverId);
            var map = Plugin.Config.Configuration().Maps.FirstOrDefault(_map => _map.Name == server.CurrentMap.Name);

            if (map != null)
            {
                map.Alias = server.CurrentMap.Alias;
                return Json(map);
            }

            // occurs if we don't recognize the map
            return StatusCode(500);
        }

        [HttpGet]
        [Route("Radar/{serverId}/Data")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Data(long? serverId = null)
        {
            var server = serverId == null ? Manager.GetServers()[0] : Manager.GetServers().First(_server => _server.EndPoint == serverId);
            var radarInfo = server.GetClientsAsList().Select(_client => _client.GetAdditionalProperty<RadarEvent>("LiveRadar")).ToList();
            return Json(radarInfo);
        }

        [HttpGet]
        [Route("Radar/Update")]
        public IActionResult Update(string payload)
        {
            var radarUpdate = RadarEvent.Parse(payload);
            var client = Manager.GetActiveClients().FirstOrDefault(_client => _client.NetworkId == radarUpdate.Guid);

            if (client != null)
            {
                radarUpdate.Name = client.Name.StripColors();
                client.SetAdditionalProperty("LiveRadar", radarUpdate);
            }

            return Ok();
        }
    }
}