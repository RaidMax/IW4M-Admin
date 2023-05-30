using System;
using System.Collections.Generic;
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
    public class Server : BaseController
    {
        private readonly IServerDataViewer _serverDataViewer;
        private readonly ApplicationConfiguration _applicationConfiguration;

        public Server(IManager manager, IServerDataViewer serverDataViewer,
            ApplicationConfiguration applicationConfiguration) : base(manager)
        {
            _serverDataViewer = serverDataViewer;
            _applicationConfiguration = applicationConfiguration;
        }
        
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

            var start = DateTime.Now;
            Client.CurrentServer = foundServer;
            
            var commandEvent = new GameEvent
            {
                Type = GameEvent.EventType.Command,
                Owner = foundServer,
                Origin = Client,
                Data = commandRequest.Command,
                Extra = commandRequest.Command,
                IsRemote = true
            };

            Manager.AddEvent(commandEvent);
            var completedEvent = await commandEvent.WaitAsync(Utilities.DefaultCommandTimeout, foundServer.Manager.CancellationToken);
            
            return new JsonResult(new
            {
                ExecutionTimeMs = Math.Round((DateTime.Now - start).TotalMilliseconds, 0),
                completedEvent.Output
            });
        }

        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetClientHistory(string id)
        {
            var foundServer = Manager.GetServers().FirstOrDefault(server => server.Id == id);
            
            if (foundServer == null)
            {
                return new NotFoundResult();
            }
            
            var clientHistory = (await _serverDataViewer.ClientHistoryAsync(_applicationConfiguration.MaxClientHistoryTime,
                                        CancellationToken.None))?
                                    .FirstOrDefault(history => history.ServerId == foundServer.LegacyDatabaseId) ??
                                new ClientHistoryInfo
                                {
                                    ServerId = foundServer.LegacyDatabaseId,
                                    ClientCounts = new List<ClientCountSnapshot>()
                                };
            
            var counts = clientHistory.ClientCounts?.AsEnumerable() ?? Enumerable.Empty<ClientCountSnapshot>();

            if (foundServer.ClientHistory.ClientCounts.Any())
            {
                counts = counts.Union(foundServer.ClientHistory.ClientCounts.Where(history =>
                        history.Time > (clientHistory.ClientCounts?.LastOrDefault()?.Time ?? DateTime.MinValue)))
                    .Where(history => history.Time >= DateTime.UtcNow - _applicationConfiguration.MaxClientHistoryTime);
            }

            if (ViewBag.Maps?.Count == 0)
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
