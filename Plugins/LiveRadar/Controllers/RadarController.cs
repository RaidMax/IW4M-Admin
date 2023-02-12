using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Linq;
using System.Threading.Tasks;
using IW4MAdmin.Plugins.LiveRadar.Configuration;
using Microsoft.AspNetCore.Http;

namespace IW4MAdmin.Plugins.LiveRadar.Web.Controllers
{
    public class RadarController : BaseController
    {
        private readonly IManager _manager;
        private readonly LiveRadarConfiguration _config;

        public RadarController(IManager manager, LiveRadarConfiguration config) :
            base(manager)
        {
            _manager = manager;
            _config = config;
        }

        [HttpGet]
        [Route("Radar/{serverId?}")]
        public IActionResult Index(string serverId = null)
        {
            var servers = _manager.GetServers()
                .Where(server => server.GameName == Server.Game.IW4)
                .Select(server => new ServerInfo
                {
                    Name = server.Hostname,
                    IPAddress = server.ListenAddress,
                    Port = server.ListenPort
                });

            ViewBag.Title = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_RADAR_TITLE"];
            ViewBag.SelectedServerId = string.IsNullOrEmpty(serverId) ? servers.FirstOrDefault()?.Endpoint : serverId;

            // ReSharper disable once Mvc.ViewNotResolved
            return View("~/Views/Plugins/LiveRadar/Radar/Index.cshtml", servers);
        }

        [HttpGet]
        [Route("Radar/{serverId}/Map")]
        public async Task<IActionResult> Map(string serverId = null)
        {
            var server = serverId == null
                ? _manager.GetServers().FirstOrDefault()
                : _manager.GetServers().FirstOrDefault(server => server.ToString() == serverId);

            if (server == null)
            {
                return NotFound();
            }

            var map = _config.Maps.FirstOrDefault(map => map.Name == server.CurrentMap.Name);

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
        public IActionResult Data(string serverId = null)
        {
            var server = serverId == null
                ? _manager.GetServers().FirstOrDefault()
                : _manager.GetServers().FirstOrDefault(server => server.ToString() == serverId);
            
            if (server == null)
            {
                return NotFound();
            }
            
            var radarInfo = server.GetClientsAsList()
                .Select(client => client.GetAdditionalProperty<RadarEvent>("LiveRadar")).ToList();
            
            return Json(radarInfo);
        }
    }
}
