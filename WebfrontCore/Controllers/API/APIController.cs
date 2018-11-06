using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Events;
using System.Linq;

namespace WebfrontCore.Controllers.API
{
    public class ApiController : BaseController
    {
        public IActionResult Index()
        {
            return Ok($"IW4MAdmin API");
        }

        [HttpGet]
        public IActionResult Event(bool shouldConsume = true)
        {
            var events = EventApi.GetEvents(shouldConsume);
            return Json(events);
        }

        [HttpGet]
        public IActionResult Status(int id)
        {
            var serverInfo = Manager.GetServers()
                .Select(server => new
                {
                    Id = server.GetHashCode(),
                    Name = server.Hostname,
                    MaxPlayers = server.MaxClients,
                    CurrentPlayers = server.GetClientsAsList().Count,
                    Map = server.CurrentMap,
                    GameMode = server.Gametype,
                    Port = server.GetPort(),
                    Game = server.GameName.ToString(),
                    Players = server.GetClientsAsList()
                        .Select(player => new
                        {
                            player.Name,
                            player.Score,
                            player.Ping,
                            State = player.State.ToString(),
                            player.ClientNumber,
                            ConnectionTime = player.ConnectionLength,
                            Level = player.Level.ToLocalizedLevelName(),
                        })
                });

            if (id != 0)
            {
                serverInfo = serverInfo.Where(server => server.Id == id);
            }

            return Json(serverInfo);
        }

        [HttpGet]
        public IActionResult RestartApproved()
        {
            var serverToRestart = Manager.GetServers().FirstOrDefault(_server => _server.RestartRequested);

            if (serverToRestart != null)
            {
                serverToRestart.RestartRequested = false;
            }

            return serverToRestart != null ? 
            (IActionResult)Json(new
            {
                port = serverToRestart.GetPort()
            }) : 
            Unauthorized();
        }
    }
}
