using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using System.Linq;
using Data.Models;
using Data.Models.Client.Stats;
using IW4MAdmin.Plugins.Stats.Helpers;
using SharedLibraryCore.Configuration;

namespace WebfrontCore.ViewComponents
{
    public class ServerListViewComponent : ViewComponent
    {
        private readonly DefaultSettings _defaultSettings;

        public ServerListViewComponent(DefaultSettings defaultSettings)
        {
            _defaultSettings = defaultSettings;
        }

        public IViewComponentResult Invoke(Reference.Game? game)
        {
            if (game.HasValue)
            {
                ViewBag.Maps = _defaultSettings.Maps?.FirstOrDefault(map => map.Game == (Server.Game)game)?.Maps
                                   ?.ToList() ?? new List<Map>();
            }
            else
            {
                ViewBag.Maps = _defaultSettings.Maps?.SelectMany(maps => maps.Maps).ToList();
            }

            var servers = Program.Manager.GetServers()
                .Where(server => game is null || server.GameName == (Server.Game)game);

            var serverInfo = new List<ServerInfo>();

            foreach (var server in servers)
            {
                serverInfo.Add(new ServerInfo
                {
                    Name = server.Hostname,
                    ID = server.EndPoint,
                    Port = server.ListenPort,
                    Map = server.CurrentMap?.Alias,
                    Game = (Reference.Game)server.GameName,
                    ClientCount = server.ClientNum,
                    MaxClients = server.MaxClients,
                    PrivateClientSlots = server.PrivateClientSlots,
                    GameType = server.GametypeName,
                    ClientHistory = new ClientHistoryInfo(),
                    Players = server.GetClientsAsList()
                        .Select(client => new PlayerInfo
                        {
                            Name = client.Name,
                            ClientId = client.ClientId,
                            Level = client.Level.ToLocalizedLevelName(),
                            LevelInt = (int)client.Level,
                            Tag = client.Tag,
                            ZScore = client.GetAdditionalProperty<EFClientStatistics>(StatManager
                                .CLIENT_STATS_KEY)?.ZScore
                        }).ToList(),
                    ChatHistory = server.ChatHistory.ToList(),
                    Online = !server.Throttled,
                    IPAddress = server.ListenAddress,
                    ExternalIPAddress = server.ResolvedIpEndPoint.Address.IsInternal() ? Program.Manager.ExternalIPAddress : server.ListenAddress,
                    ConnectProtocolUrl = server.EventParser.URLProtocolFormat.FormatExt(
                        server.ResolvedIpEndPoint.Address.IsInternal() ? Program.Manager.ExternalIPAddress : server.ListenAddress,
                        server.ListenPort)
                });
            }

            return View("_List", serverInfo);
        }
    }
}
