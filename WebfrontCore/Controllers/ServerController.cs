using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Linq;
using Data.Models;
using Data.Models.Client.Stats;
using IW4MAdmin.Plugins.Stats.Helpers;
using WebfrontCore.ViewModels;

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
            var matchingServer = Manager.GetServers().FirstOrDefault(server => server.EndPoint == id);

            if (matchingServer == null)
            {
                return NotFound();
            }

            var serverInfo = new ServerInfo
            {
                Name = matchingServer.Hostname,
                ID = matchingServer.EndPoint,
                Port = matchingServer.ListenPort,
                Map = matchingServer.CurrentMap?.Alias,
                Game = (Reference.Game)matchingServer.GameName,
                ClientCount = matchingServer.ClientNum,
                MaxClients = matchingServer.MaxClients,
                GameType = matchingServer.GametypeName,
                Players = matchingServer.GetClientsAsList()
                    .Select(client => new PlayerInfo
                    {
                        Name = client.Name,
                        ClientId = client.ClientId,
                        Level = client.Level.ToLocalizedLevelName(),
                        LevelInt = (int)client.Level,
                        ZScore = client.GetAdditionalProperty<EFClientStatistics>(StatManager
                            .CLIENT_STATS_KEY)?.ZScore
                    }).ToList(),
                ChatHistory = matchingServer.ChatHistory.ToList(),
                ClientHistory = matchingServer.ClientHistory,
                IsPasswordProtected = !string.IsNullOrEmpty(matchingServer.GamePassword)
            };
            return PartialView("_ClientActivity", serverInfo);
        }

        [HttpGet]
        public ActionResult Scoreboard(string serverId)
        {
            ViewBag.Title = Localization["WEBFRONT_TITLE_SCOREBOARD"];
            ViewBag.SelectedServerId = string.IsNullOrEmpty(serverId) ? Manager.GetServers().FirstOrDefault()?.ToString() : serverId;
            
            return View(ProjectScoreboard(Manager.GetServers(), null, true));
        }

        [HttpGet("[controller]/{id}/scoreboard")]
        public ActionResult Scoreboard(string id, [FromQuery]string order = null, [FromQuery] bool down = true)
        {
            
            var server = Manager.GetServers().FirstOrDefault(srv => srv.ToString() == id);

            if (server == null)
            {
                return NotFound();
            }

            ViewBag.SelectedServerId = id;
            return View("_Scoreboard", ProjectScoreboard(new[] {server}, order, down).First());
        }

        private static IEnumerable<ScoreboardInfo> ProjectScoreboard(IEnumerable<Server> servers, string order,
            bool down)
        {
            return servers.Select((server, index) => new ScoreboardInfo
            {
                OrderByKey = order,
                ShouldOrderDescending = down,
                MapName = server.CurrentMap.ToString(),
                ServerName = server.Hostname,
                ServerId = server.ToString(),
                ClientInfo = server.GetClientsAsList().Select(client =>
                        new
                        {
                            stats = client.GetAdditionalProperty<EFClientStatistics>(StatManager.CLIENT_STATS_KEY),
                            client
                        })
                    .Select(clientData => new ClientScoreboardInfo
                    {
                        ClientName = clientData.client.Name,
                        ClientId = clientData.client.ClientId,
                        Score = Math.Max(clientData.client.Score, clientData.stats?.RoundScore ?? 0),
                        Ping = clientData.client.Ping,
                        Kills = clientData.stats?.MatchData?.Kills,
                        Deaths = clientData.stats?.MatchData?.Deaths,
                        ScorePerMinute = clientData.stats?.SessionSPM,
                        Kdr = clientData.stats?.MatchData?.Kdr,
                        ZScore = clientData.stats?.ZScore == null || clientData.stats.ZScore == 0 ? null : clientData.stats.ZScore,
                        Team = clientData.client.Team
                    })
                    .ToList()
            }).ToList();
        }
    }
}
