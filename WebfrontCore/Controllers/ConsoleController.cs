using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.Controllers
{
    public class ConsoleController : BaseController
    {
        public IActionResult Index()
        {
            var activeServers = Manager.Servers.Select(s => new ServerInfo()
            {
                Name = s.Hostname,
                ID = s.GetHashCode(),
            });

            ViewBag.Description = "Use the IW4MAdmin web console to execute commands";
            ViewBag.Title = "Web Console";
            ViewBag.Keywords = "IW4MAdmin, console, execute, commands";

            return View(activeServers);
        }

        public async Task<IActionResult> ExecuteAsync(int serverId, string command)
        {

            var server = Manager.Servers.First(s => s.GetHashCode() == serverId);
            var client = new Player()
            {
                ClientId = User.ClientId,
                Level = User.Level,
                CurrentServer = server
            };

            var remoteEvent = new Event(Event.GType.Say, command, client, null, server);

            await server.ExecuteEvent(remoteEvent);

            var response = server.CommandResult.Where(c => c.ClientId == client.ClientId).ToList();

            // remove the added command response
            for (int i = 0; i < response.Count; i++)
                server.CommandResult.Remove(response[i]);

            return View("_Response", response);
        }
    }
}
