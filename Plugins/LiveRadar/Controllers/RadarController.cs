using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System.Linq;
using IW4MAdmin.Plugins.LiveRadar.Configuration;
using Microsoft.AspNetCore.Http;

namespace IW4MAdmin.Plugins.LiveRadar.Web.Controllers
{
    public class RadarController(IManager manager, LiveRadarConfiguration config) : BaseController(manager)
    {
        private readonly IManager _manager = manager;


        [HttpGet]
        [Route("Radar/{serverId}/Map")]
        public IActionResult Map(string serverId = null)
        {
            var server = serverId == null
                ? _manager.GetServers().FirstOrDefault()
                : _manager.GetServers().FirstOrDefault(server => server.ToString() == serverId);

            if (server == null)
            {
                return NotFound();
            }

            var map = config.Maps.FirstOrDefault(map => map.Name == server.CurrentMap.Name);

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
                .Select(client => client.GetAdditionalProperty<RadarDto>("LiveRadar")).ToList();

            return Json(radarInfo);
        }
    }
}
