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

            var serverInfo = new ServerInfo()
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
                        ZScore = p.GetAdditionalProperty<EFClientStatistics>(IW4MAdmin.Plugins.Stats.Helpers.StatManager
                            .CLIENT_STATS_KEY)?.ZScore
                    }).ToList(),
                ChatHistory = s.ChatHistory.ToList(),
                PlayerHistory = s.ClientHistory.ToArray(),
                IsPasswordProtected = !string.IsNullOrEmpty(s.GamePassword)
            };
            return PartialView("_ClientActivity", serverInfo);
        }

        [HttpGet]
        public ActionResult Scoreboard()
        {
            ViewBag.Title = Localization["WEBFRONT_TITLE_SCOREBOARD"];
            
            return View(ProjectScoreboard(Manager.GetServers()));
        }

        [HttpGet("[controller]/{id}/scoreboard")]
        public ActionResult Scoreboard(long id)
        {
            var server = Manager.GetServers().FirstOrDefault(srv => srv.EndPoint == id);

            if (server == null)
            {
                return NotFound();
            }

            return View("_Scoreboard", ProjectScoreboard(new[] {server}).First());
        }

        private IEnumerable<ScoreboardInfo> ProjectScoreboard(IEnumerable<Server> servers)
        {
            return servers.Select(server => new ScoreboardInfo
            {
                MapName = server.CurrentMap.ToString(),
                ServerName = server.Hostname,
                ServerId = server.EndPoint,
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
                        Score = clientData.client.Score,
                        Ping = clientData.client.Ping,
                        Kills = clientData.stats?.MatchData?.Kills,
                        Deaths = clientData.stats?.MatchData?.Deaths,
                        ScorePerMinute = clientData.stats?.SessionSPM,
                        Kdr = clientData.stats?.MatchData?.Kdr
                    })
                    .ToList()
            }).ToList();
        }
    }
}
