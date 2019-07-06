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
        private static readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
        };

        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.IsFluid = true;
            return View();
        }

        public IActionResult Map(long? serverId = null)
        {
            var server = Manager.GetServers().FirstOrDefault();

            var map = Plugin.Config.Configuration().Maps.FirstOrDefault(_map => _map.Name == server.CurrentMap.Name);

            return Json(map);
        }

        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Data(long? serverId = null)
        {
            var server = serverId == null ? Manager.GetServers()[0] : Manager.GetServers().First(_server => _server.GetHashCode() == serverId);
            var radarInfo = server.GetClientsAsList().Select(_client => _client.GetAdditionalProperty<RadarEvent>("LiveRadar")).ToList();
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
                client.SetAdditionalProperty("LiveRadar", radarUpdate);
            }

            return Ok();
        }
    }
}