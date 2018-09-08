using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.Controllers.API
{
    public class ApiController : BaseController
    {
        public IActionResult Index() => Ok($"IW4MAdmin API");

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
                    CurrentPlayers = server.GetPlayersAsList().Count,
                    Map = server.CurrentMap,
                    GameMode = server.Gametype,
                    Port = server.GetPort(),
                    Game = server.GameName.ToString(),
                    Players = server.GetPlayersAsList()
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
    }
}
