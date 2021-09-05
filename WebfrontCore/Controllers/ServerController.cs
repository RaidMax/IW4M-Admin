using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Linq;
using Data.Models.Client.Stats;

namespace WebfrontCore.Controllers
{
    public class ServerController : BaseController
    {
        public ServerController(IManager manager) : base(manager)
        {

        }

        [HttpGet]
        [ResponseCache(NoStore = true, Duration = 0)]
        public IActionResult ClientActivity(long id)
        {
            var s = Manager.GetServers().FirstOrDefault(_server => _server.EndPoint == id);

            if (s == null)
            {
                return NotFound();
            }

            var serverInfo = new ServerInfo()
            {
                Name = s.Hostname,
                ID = s.EndPoint,
                Port = s.Port,
                Map = s.CurrentMap.Alias,
                ClientCount = s.Clients.Count(client => client != null),
                MaxClients = s.MaxClients,
                GameType = s.Gametype,
                Players = s.GetClientsAsList()
                .Select(p => new PlayerInfo
                {
                    Name = p.Name,
                    ClientId = p.ClientId,
                    Level = p.Level.ToLocalizedLevelName(),
                    LevelInt = (int)p.Level,
                    ZScore = p.GetAdditionalProperty<EFClientStatistics>(IW4MAdmin.Plugins.Stats.Helpers.StatManager.CLIENT_STATS_KEY)?.ZScore
                }).ToList(),
                ChatHistory = s.ChatHistory.ToList(),
                PlayerHistory = s.ClientHistory.ToArray(),
                IsPasswordProtected = !string.IsNullOrEmpty(s.GamePassword)
            };
            return PartialView("_ClientActivity", serverInfo);
        }
    }
}
