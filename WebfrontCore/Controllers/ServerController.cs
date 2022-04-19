using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Linq;
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
            var s = Manager.GetServers().FirstOrDefault(_server => _server.EndPoint == id);

            if (s == null)
            {
                return NotFound();
            }

            var serverInfo = new ServerInfo
            {
                Name = s.Hostname,
                ID = s.EndPoint,
                Port = s.Port,
                Map = s.CurrentMap.Alias,
                ClientCount = s.Clients.Count(client => client != null),
                MaxClients = s.MaxClients,
                GameType = s.GametypeName,
                Players = s.GetClientsAsList()
                    .Select(p => new PlayerInfo
                    {
                        Name = p.Name,
                        ClientId = p.ClientId,
                        Level = p.Level.ToLocalizedLevelName(),
                        LevelInt = (int) p.Level,
                        ZScore = p.GetAdditionalProperty<EFClientStatistics>(StatManager
                            .CLIENT_STATS_KEY)?.ZScore
                    }).ToList(),
                ChatHistory = s.ChatHistory.ToList(),
                ClientHistory = s.ClientHistory,
                IsPasswordProtected = !string.IsNullOrEmpty(s.GamePassword)
            };
            return PartialView("_ClientActivity", serverInfo);
        }

        [HttpGet]
        public ActionResult Scoreboard(string serverId)
        {
            ViewBag.Title = Localization["WEBFRONT_TITLE_SCOREBOARD"];
            ViewBag.SelectedServerId = string.IsNullOrEmpty(serverId) ? Manager.GetServers().FirstOrDefault()?.ToString() : serverId;
            
            return View(ProjectScoreboard(Manager.GetServers(), null, true, false));
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
            bool down, bool includeDetails = true)
        {
            return servers.Select((server, index) => new ScoreboardInfo
            {
                OrderByKey = order,
                ShouldOrderDescending = down,
                MapName = server.CurrentMap.ToString(),
                ServerName = server.Hostname,
                ServerId = server.ToString(),
                ClientInfo = index == 0 && !includeDetails || includeDetails ? server.GetClientsAsList().Select(client =>
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
                    .ToList() : new List<ClientScoreboardInfo>()
            }).ToList();
        }
    }
}
