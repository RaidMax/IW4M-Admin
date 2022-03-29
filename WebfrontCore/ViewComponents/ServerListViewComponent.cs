using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Data.Models.Client.Stats;
using Microsoft.AspNetCore.Hosting.Server;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using static SharedLibraryCore.Server;

namespace WebfrontCore.ViewComponents
{
    public class ServerListViewComponent : ViewComponent
    {
        private readonly IServerDataViewer _serverDataViewer;
        private readonly ApplicationConfiguration _appConfig;
        private readonly DefaultSettings _defaultSettings;

        public ServerListViewComponent(IServerDataViewer serverDataViewer,
            ApplicationConfiguration applicationConfiguration, DefaultSettings defaultSettings)
        {
            _serverDataViewer = serverDataViewer;
            _appConfig = applicationConfiguration;
            _defaultSettings = defaultSettings;
        }

        public IViewComponentResult Invoke(Game? game)
        {
            if (game.HasValue)
            {
                ViewBag.Maps = _defaultSettings.Maps.FirstOrDefault(map => map.Game == game);
            }
            else
            {
                ViewBag.Maps = _defaultSettings.Maps.SelectMany(maps => maps.Maps).ToList();
            }

            var servers = Program.Manager.GetServers().Where(server => !game.HasValue || server.GameName == game);

            var serverInfo = new List<ServerInfo>();

            foreach (var server in servers)
            {
                var serverId = server.GetIdForServer().Result;
                var clientHistory = _serverDataViewer.ClientHistoryAsync(_appConfig.MaxClientHistoryTime,
                                            CancellationToken.None).Result?
                                        .FirstOrDefault(history => history.ServerId == serverId) ??
                                    new ClientHistoryInfo
                                    {
                                        ServerId = serverId,
                                        ClientCounts = new List<ClientCountSnapshot>()
                                    };

                var counts = clientHistory.ClientCounts?.AsEnumerable() ?? Enumerable.Empty<ClientCountSnapshot>();

                if (server.ClientHistory.ClientCounts.Any())
                {
                    counts = counts.Union(server.ClientHistory.ClientCounts.Where(history =>
                            history.Time > (clientHistory.ClientCounts?.LastOrDefault()?.Time ?? DateTime.MinValue)))
                        .Where(history => history.Time >= DateTime.UtcNow - _appConfig.MaxClientHistoryTime);
                }

                serverInfo.Add(new ServerInfo
                {
                    Name = server.Hostname,
                    ID = server.EndPoint,
                    Port = server.Port,
                    Map = server.CurrentMap.Alias,
                    ClientCount = server.Clients.Count(client => client != null),
                    MaxClients = server.MaxClients,
                    GameType = server.GametypeName,
                    ClientHistory = new ClientHistoryInfo
                    {
                        ServerId = server.EndPoint,
                        ClientCounts = counts.ToList()
                    },
                    Players = server.GetClientsAsList()
                        .Select(p => new PlayerInfo()
                        {
                            Name = p.Name,
                            ClientId = p.ClientId,
                            Level = p.Level.ToLocalizedLevelName(),
                            LevelInt = (int) p.Level,
                            Tag = p.Tag,
                            ZScore = p.GetAdditionalProperty<EFClientStatistics>(IW4MAdmin.Plugins.Stats.Helpers
                                .StatManager
                                .CLIENT_STATS_KEY)?.ZScore
                        }).ToList(),
                    ChatHistory = server.ChatHistory.ToList(),
                    Online = !server.Throttled,
                    IPAddress =
                        $"{(server.ResolvedIpEndPoint.Address.IsInternal() ? Program.Manager.ExternalIPAddress : server.IP)}:{server.Port}",
                    ConnectProtocolUrl = server.EventParser.URLProtocolFormat.FormatExt(
                        server.ResolvedIpEndPoint.Address.IsInternal() ? Program.Manager.ExternalIPAddress : server.IP,
                        server.Port)
                });
            }

            return View("_List", serverInfo);
        }
    }
}
