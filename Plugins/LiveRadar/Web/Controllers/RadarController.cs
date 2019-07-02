using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebfrontCore.Controllers;

namespace LiveRadar.Web.Controllers
{
    public class RadarController : BaseController
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Map(long? serverId = null)
        {
            var map = new MapInfo()
            {
                Name = "mp_rust",
                Top = 248,
                Bottom = 212,
                Left = 314,
                Right = 167,
                MaxRight = -225,
                MaxLeft = 1809,
                MaxTop = 1641,
                MaxBottom = -469
            };

            return Json(map);
        }

        public IActionResult Data(long? serverId = null)
        {
            var server = serverId == null ? Manager.GetServers()[0] : Manager.GetServers().First(_server => _server.GetHashCode() == serverId);
            var radarInfo = server.GetClientsAsList().Select(_client => _client.GetAdditionalProperty<RadarEvent>("LiveRadar"));
            return Json(radarInfo);
        }

        [HttpGet]
        public IActionResult Update(string payload)
        {
            var radarUpdate = RadarEvent.Parse(payload);
            var client = Manager.GetActiveClients().FirstOrDefault(_client => _client.NetworkId == radarUpdate.Guid);

            if (client != null)
            {
                radarUpdate.Name = client.Name;
                var previous = client.GetAdditionalProperty<RadarEvent>("LiveRadar");
                // this prevents us from creating a never ending linked list
                if (previous != null)
                {
                    previous.Previous = null;
                }

                radarUpdate.Previous = previous;
                client.SetAdditionalProperty("LiveRadar", radarUpdate);
            }

            return Ok();
        }
    }
}
