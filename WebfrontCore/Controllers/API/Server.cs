using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Controllers.API.Models;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class Server : BaseController
    {
        
        public Server(IManager manager) : base(manager)
        {
        }
        
        [HttpGet]
        public IActionResult Index()
        {
            return new JsonResult(Manager.GetServers().Select(server => new
            {
                Id = server.EndPoint,
                server.Hostname,
                server.IP,
                server.Port
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
                foundServer.Hostname,
                foundServer.IP,
                foundServer.Port
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
            
            var commandEvent = new GameEvent()
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
    }
}