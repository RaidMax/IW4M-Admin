using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using Data.Models.Client.Stats;
using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatusController : BaseController
    {
        private readonly IServerDataViewer _serverDataViewer;
        private readonly DefaultSettings _defaultSettings;

        public StatusController(IManager manager, IServerDataViewer serverDataViewer, DefaultSettings defaultSettings) : base(manager)
        {
            _serverDataViewer = serverDataViewer;
            _defaultSettings = defaultSettings;
        }

        [HttpGet]
        public async Task<ActionResult<IW4MAdminInfo>> GetStatus([FromQuery] Reference.Game? game = null, CancellationToken cancellationToken = default)
        {
            var servers = Manager.GetServers().Where(server => game is null || server.GameName == (SharedLibraryCore.Server.Game?)game)
                .ToList();
            var (clientCount, time) =
                await _serverDataViewer.MaxConcurrentClientsAsync(gameCode: game, token: cancellationToken);
            var (count, recentCount) =
                await _serverDataViewer.ClientCountsAsync(gameCode: game, token: cancellationToken);

            var model = new IW4MAdminInfo
            {
                TotalAvailableClientSlots = servers.Sum(server => server.MaxClients),
                TotalOccupiedClientSlots = servers.SelectMany(server => server.GetClientsAsList()).Count(),
                TotalClientCount = count,
                RecentClientCount = recentCount,
                MaxConcurrentClients = clientCount ?? 0,
                MaxConcurrentClientsTime = time ?? DateTime.UtcNow,
                Game = game,
                ActiveServerGames = Manager.GetServers().Select(server => (Reference.Game)server.GameName).Distinct()
                    .ToArray(),
                CommandPrefix = Manager.GetApplicationSettings().Configuration().CommandPrefix
            };

            return Ok(model);
        }

        [HttpGet("servers")]
        public ActionResult<IEnumerable<ServerInfo>> GetServers([FromQuery] Reference.Game? game = null)
        {
            var servers = Manager.GetServers()
                .Where(server => game is null || server.GameName == (SharedLibraryCore.Server.Game)game);

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
                    ExternalIPAddress = server.ResolvedIpEndPoint.Address.IsInternal() ? Manager.ExternalIPAddress : server.ListenAddress,
                    ConnectProtocolUrl = server.EventParser.URLProtocolFormat.FormatExt(
                        server.ResolvedIpEndPoint.Address.IsInternal() ? Manager.ExternalIPAddress : server.ListenAddress,
                        server.ListenPort)
                });
            }

            return Ok(serverInfo);
        }

        [HttpGet("servers/{id}")]
        public ActionResult<ServerInfo> GetServer(long id)
        {
            var server = Manager.GetServers().FirstOrDefault(s => s.EndPoint == id);
            if (server == null) return NotFound();

            return Ok(new ServerInfo
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
                     // ClientHistory property on server object is ServerClientHistory type? 
                     // We need to map it. Existing controller mapped: ClientHistory = new ClientHistoryInfo() then populated later?
                     // API Server.cs uses clientHistoryAsync logic.
                     // For simplicity, we assume we can just pass server.ClientHistory.ClientCounts if available.
                     ClientCounts = server.ClientHistory.ClientCounts.ToList()
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
                ExternalIPAddress = server.ResolvedIpEndPoint.Address.IsInternal() ? Manager.ExternalIPAddress : server.ListenAddress,
                ConnectProtocolUrl = server.EventParser.URLProtocolFormat.FormatExt(
                        server.ResolvedIpEndPoint.Address.IsInternal() ? Manager.ExternalIPAddress : server.ListenAddress,
                        server.ListenPort)
            });
        }

        [HttpGet("server/{id}/scoreboard")]
        public ActionResult<WebfrontCore.ViewModels.ScoreboardInfo> GetScoreboard(long id)
        {
            var server = Manager.GetServers().FirstOrDefault(s => s.EndPoint == id);
            if (server == null) return NotFound();

            return Ok(new WebfrontCore.ViewModels.ScoreboardInfo
            {
                MapName = server.CurrentMap.ToString(),
                ServerName = server.Hostname,
                ServerId = server.ToString(), // ServerId in viewmodel is string
                GameCode = server.GameCode,
                ClientInfo = server.GetClientsAsList().Select(client =>
                        new
                        {
                            stats = client.GetAdditionalProperty<EFClientStatistics>(StatManager.CLIENT_STATS_KEY),
                            client
                        })
                    .Select(clientData => new WebfrontCore.ViewModels.ClientScoreboardInfo
                    {
                        ClientName = clientData.client.Name,
                        ClientId = clientData.client.ClientId,
                        Score = Math.Max(clientData.client.Score, clientData.stats?.RoundScore ?? 0),
                        Ping = clientData.client.Ping,
                        Kills = clientData.stats?.MatchData?.Kills,
                        Deaths = clientData.stats?.MatchData?.Deaths,
                        ScorePerMinute = clientData.stats?.SessionSPM,
                        Kdr = clientData.stats?.MatchData?.Kdr,
                        ZScore = clientData.stats?.ZScore == null || clientData.stats.ZScore == 0
                            ? null
                            : clientData.stats.ZScore,
                        Team = clientData.client.Team
                    })
                    .ToList()
            });
        }
    }
}
