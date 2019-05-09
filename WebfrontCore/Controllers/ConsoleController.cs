using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.Controllers
{
    public class ConsoleController : BaseController
    {
        public IActionResult Index()
        {
            var activeServers = Manager.GetServers().Select(s => new ServerInfo()
            {
                Name = s.Hostname,
                ID = s.EndPoint,
            });

            ViewBag.Description = "Use the IW4MAdmin web console to execute commands";
            ViewBag.Title = Localization["WEBFRONT_CONSOLE_TITLE"];
            ViewBag.Keywords = "IW4MAdmin, console, execute, commands";

            return View(activeServers);
        }

        public async Task<IActionResult> ExecuteAsync(long serverId, string command)
        {
            var server = Manager.GetServers().First(s => s.EndPoint == serverId);

            var client = new EFClient()
            {
                ClientId = Client.ClientId,
                Level = Client.Level,
                NetworkId = Client.NetworkId,
                CurrentServer = server,
                CurrentAlias = new EFAlias()
                {
                    Name = Client.Name
                }
            };

            var remoteEvent = new GameEvent()
            {
                Type = GameEvent.EventType.Command,
                Data = command,
                Origin = client,
                Owner = server,
                IsRemote = true
            };

            Manager.GetEventHandler().AddEvent(remoteEvent);
            List<CommandResponseInfo> response;
            // wait for the event to process
            if (!(await remoteEvent.WaitAsync(Utilities.DefaultCommandTimeout, server.Manager.CancellationToken)).Failed)
            {
                response = server.CommandResult.Where(c => c.ClientId == client.ClientId).ToList();

                // remove the added command response
                for (int i = 0; i < response.Count; i++)
                {
                    server.CommandResult.Remove(response[i]);
                }
            }

            else
            {
                response = new List<CommandResponseInfo>()
                {
                    new CommandResponseInfo()
                    {
                        ClientId = client.ClientId,
                        Response = Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_COMMAND_TIMEOUT"]
                    }
                };
            }

            return View("_Response", response);
        }
    }
}
