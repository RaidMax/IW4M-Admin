using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Controllers.API.Models;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class Server(
        IManager manager,
        IServerDataViewer serverDataViewer,
        ApplicationConfiguration applicationConfiguration,
        IRemoteCommandService remoteCommandService)
        : BaseController(manager)
    {
        [HttpGet]
        public IActionResult Index()
        {
            return new JsonResult(Manager.GetServers().Select(server => new
            {
                Id = server.EndPoint,
                server.ServerName,
                server.ListenAddress,
                server.ListenPort,
                Game = server.GameName.ToString(),
                server.ClientNum,
                server.MaxClients,
                server.CurrentMap,
                currentGameType = new
                {
                    type = server.Gametype,
                    name = server.GametypeName
                },
                Parser = server.RconParser.Name,
            }));
        }

        [HttpGet("{id}")]
        public IActionResult GetServerById(string id)
        {
            var foundServer = Manager.GetServers().FirstOrDefault(server => server.EndPoint == long.Parse(id));

            if (foundServer == null)
            {
                return new NotFoundResult();
            }

            return new JsonResult(new
            {
                Id = foundServer.EndPoint,
                foundServer.ServerName,
                foundServer.ListenAddress,
                foundServer.ListenPort,
                Game = foundServer.GameName.ToString(),
                foundServer.ClientNum,
                foundServer.MaxClients,
                foundServer.CurrentMap,
                currentGameType = new
                {
                    type = foundServer.Gametype,
                    name = foundServer.GametypeName
                },
                Parser = foundServer.RconParser.Name,
            });
        }

        [HttpPost("{id}/execute")]
        public async Task<IActionResult> ExecuteCommandForServer(string id, [FromBody] CommandRequest commandRequest)
        {
            if (!Authorized)
            {
                return Unauthorized();
            }

            var foundServer = Manager.GetServers().FirstOrDefault(server => server.EndPoint == long.Parse(id));

            if (foundServer == null)
            {
                return new BadRequestObjectResult($"No server with id '{id}' was found");
            }

            if (string.IsNullOrEmpty(commandRequest.Command))
            {
                return new BadRequestObjectResult("Command cannot be empty");
            }

            var start = TimeProvider.System.GetLocalNow();
            Client.CurrentServer = foundServer;

            var completedResult =
                await remoteCommandService.ExecuteWithResult(Client.ClientId, null, commandRequest.Command, null, foundServer);

            return new JsonResult(new
            {
                ExecutionTimeMs = Math.Round((TimeProvider.System.GetLocalNow() - start).TotalMilliseconds, 0),
                Output = completedResult.Item2.Where(x => !string.IsNullOrWhiteSpace(x.Response))
                    .Select(x => x.Response.Trim())
            });
        }

        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetClientHistory(string id)
        {
            var foundServer = Manager.GetServers().FirstOrDefault(server => server.Id == id);

            if (foundServer is null)
            {
                return new NotFoundResult();
            }

            var clientHistory =
                (await serverDataViewer.ClientHistoryAsync(applicationConfiguration.MaxClientHistoryTime, CancellationToken.None))?
                .FirstOrDefault(history => history.ServerId == foundServer.LegacyDatabaseId) ??
                new ClientHistoryInfo
                {
                    ServerId = foundServer.LegacyDatabaseId,
                    ClientCounts = []
                };

            var counts = clientHistory.ClientCounts?.AsEnumerable() ?? [];

            if (foundServer.ClientHistory.ClientCounts.Count is not 0)
            {
                counts = counts.Union(foundServer.ClientHistory.ClientCounts.Where(history =>
                        history.Time > (clientHistory.ClientCounts?.LastOrDefault()?.Time ?? DateTime.MinValue)))
                    .Where(history => history.Time >= DateTime.UtcNow - applicationConfiguration.MaxClientHistoryTime);
            }

            if (ViewBag.Maps?.Count is 0)
            {
                return Json(counts.ToList());
            }

            var clientCountSnapshots = counts.ToList();
            foreach (var count in clientCountSnapshots)
            {
                count.MapAlias = foundServer.Maps.FirstOrDefault(map => map.Name == count.Map)?.Alias ??
                                 count.Map;
            }

            return Json(clientCountSnapshots);
        }
    }
}
