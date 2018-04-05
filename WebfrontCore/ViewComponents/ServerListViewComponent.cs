using Microsoft.AspNetCore.Mvc;
using SharedLibrary.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.ViewComponents
{
    public class ServerListViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke()
        {
            var servers = IW4MAdmin.Program.ServerManager.GetServers();
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
                Players = s.Players.Where(p => p != null)
                .Select(p => new PlayerInfo()
                {
                    Name = p.Name,
                    ClientId = p.ClientId,
                    Level = p.Level.ToString(),
                    LevelInt = (int)p.Level
                }).ToList(),
                ChatHistory = s.ChatHistory.ToArray(),
                Online = !s.Throttled
            }).ToList();
            return View("_List", serverInfo);
        }
    }
}
