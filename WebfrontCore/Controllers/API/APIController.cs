using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System.Linq;

namespace WebfrontCore.Controllers.API;

public class ApiController : BaseController
{
    public ApiController(IManager manager) : base(manager)
    {

    }

    public IActionResult Index()
    {
        return Ok("IW4MAdmin API");
    }

    [HttpGet]
    public IActionResult Event()
    {
        return NotFound("This API endpoint is no longer supported");
    }

    [HttpGet]
    public IActionResult Status(long? id)
    {
        var serverInfo = Manager.GetServers()
            .Select(server => new
            {
                Id = server.EndPoint,
                IsOnline = !server.Throttled,
                Name = server.ServerName,
                MaxPlayers = server.MaxClients,
                CurrentPlayers = server.GetClientsAsList().Count,
                Map = server.CurrentMap,
                GameMode = server.Gametype,
                server.ListenAddress,
                server.ListenPort,
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

        if (id != null)
        {
            serverInfo = serverInfo.Where(server => server.Id == id);
        }

        return Json(serverInfo);
    }
}
