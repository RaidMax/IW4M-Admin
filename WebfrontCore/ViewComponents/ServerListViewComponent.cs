using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using System.Linq;
using System.Threading;
using Data.Models;
using Data.Models.Client.Stats;
using IW4MAdmin.Plugins.Stats.Helpers;
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
                ViewBag.Maps = _defaultSettings.Maps.FirstOrDefault(map => map.Game == game)?.Maps.ToList() ??
                               new List<Map>();
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
                    Port = server.ListenPort,
                    Map = server.CurrentMap?.Alias,
                    Game = (Reference.Game)server.GameName,
                    ClientCount = server.ClientNum,
                    MaxClients = server.MaxClients,
                    PrivateClientSlots = server.PrivateClientSlots,
                    GameType = server.GametypeName,
                    ClientHistory = new ClientHistoryInfo
                    {
                        ServerId = server.EndPoint,
                        ClientCounts = counts.ToList()
                    },
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
