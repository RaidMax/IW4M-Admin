using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Dtos;
using System.Linq;

namespace WebfrontCore.ViewComponents
{
    public class ServerListViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke()
        {
            var servers = Program.Manager.GetServers();
            var serverInfo = servers.Select(s => new ServerInfo()
            {
                Name = s.Hostname,
                ID = s.GetHashCode(),
                Port = s.GetPort(),
                Map = s.CurrentMap.Alias,
                ClientCount = s.ClientNum,
                MaxClients = s.MaxClients,
                GameType = s.Gametype,
                PlayerHistory = s.PlayerHistory.ToArray(),
                Players = s.GetPlayersAsList()
                .Select(p => new PlayerInfo()
                {
                    Name = p.Name,
                    ClientId = p.ClientId,
                    Level = p.Level.ToString(),
                    LevelInt = (int)p.Level
                }).ToList(),
                ChatHistory = s.ChatHistory,
                Online = !s.Throttled
            }).ToList();
            return View("_List", serverInfo);
        }
    }
}
