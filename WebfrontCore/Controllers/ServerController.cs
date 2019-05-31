using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using System.Linq;

namespace WebfrontCore.Controllers
{
    public class ServerController : BaseController
    {
        [HttpGet]
        [ResponseCache(NoStore = true, Duration = 0)]
        public IActionResult ClientActivity(long id)
        {
            var s = Manager.GetServers().FirstOrDefault(_server => _server.EndPoint == id);

            if (s == null)
            {
                return View("Error", "Invalid server!");
            }

            var serverInfo = new ServerInfo()
            {
                Name = s.Hostname,
                ID = s.EndPoint,
                Port = s.Port,
                Map = s.CurrentMap.Alias,
                ClientCount = s.ClientNum,
                MaxClients = s.MaxClients,
                GameType = s.Gametype,
                Players = s.GetClientsAsList()
                .Select(p => new PlayerInfo
                {
                    Name = p.Name,
                    ClientId = p.ClientId,
                    Level = p.Level.ToLocalizedLevelName(),
                    LevelInt = (int)p.Level
                }).ToList(),
                ChatHistory = s.ChatHistory.ToList(),
                PlayerHistory = s.ClientHistory.ToArray()
            };
            return PartialView("_ClientActivity", serverInfo);
        }
    }
}
