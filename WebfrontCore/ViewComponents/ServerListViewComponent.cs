using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using System.Linq;
using System.Net;
using Data.Models.Client.Stats;
using static SharedLibraryCore.Server;

namespace WebfrontCore.ViewComponents
{
    public class ServerListViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(Game? game)
        {
            var servers = Program.Manager.GetServers().Where(_server => !game.HasValue || _server.GameName == game);

            var serverInfo = servers.Select(s => new ServerInfo()
            {
                Name = s.Hostname,
                ID = s.EndPoint,
                Port = s.Port,
                Map = s.CurrentMap.Alias,
                ClientCount = s.ClientNum,
                MaxClients = s.MaxClients,
                GameType = s.Gametype,
                PlayerHistory = s.ClientHistory.ToArray(),
                Players = s.GetClientsAsList()
                .Select(p => new PlayerInfo()
                {
                    Name = p.Name,
                    ClientId = p.ClientId,
                    Level = p.Level.ToLocalizedLevelName(),
                    LevelInt = (int)p.Level,
                    Tag = p.Tag,
                    ZScore = p.GetAdditionalProperty<EFClientStatistics>(IW4MAdmin.Plugins.Stats.Helpers.StatManager.CLIENT_STATS_KEY)?.ZScore
                }).ToList(),
                ChatHistory = s.ChatHistory.ToList(),
                Online = !s.Throttled,
                IPAddress = $"{(s.ResolvedIpEndPoint.Address.IsInternal() ? Program.Manager.ExternalIPAddress : s.IP)}:{s.Port}",
                ConnectProtocolUrl = s.EventParser.URLProtocolFormat.FormatExt(s.ResolvedIpEndPoint.Address.IsInternal() ? Program.Manager.ExternalIPAddress : s.IP, s.Port)
            }).ToList();
            return View("_List", serverInfo);
        }
    }
}
