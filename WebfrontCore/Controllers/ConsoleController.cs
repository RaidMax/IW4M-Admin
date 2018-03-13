using Microsoft.AspNetCore.Mvc;
using SharedLibrary;
using SharedLibrary.Dtos;
using SharedLibrary.Objects;
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
            var requestIPAddress = Request.HttpContext.Connection.RemoteIpAddress;
            var intIP = requestIPAddress.ToString().ConvertToIP();

#if !DEBUG
            var origin = (await IW4MAdmin.ApplicationManager.GetInstance().GetClientService().GetClientByIP(intIP))
                .OrderByDescending(c => c.Level)
                .FirstOrDefault()?.AsPlayer() ?? new Player()
                {
                    Name = "WebConsoleUser",
                    Level = Player.Permission.User,
                    IPAddress = intIP
                };
#else
                   var origin = (await Manager.GetClientService().GetUnique(0)).AsPlayer();
#endif

            var server = Manager.Servers.First(s => s.GetHashCode() == serverId);
            origin.CurrentServer = server;
            var remoteEvent = new Event(Event.GType.Say, command, origin, null, server);

            await server.ExecuteEvent(remoteEvent);

            var response = server.CommandResult.Where(c => c.ClientId == origin.ClientId).ToList();

            // remove the added command response
            for (int i = 0; i < response.Count; i++)
                server.CommandResult.Remove(response[i]);

            return View("_Response", response);
        }
    }
}
