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
    public class ConsoleController : Controller
    {
        public IActionResult Index()
        {
            var activeServers = IW4MAdmin.ApplicationManager.GetInstance().Servers.Select(s => new ServerInfo()
            {
                Name = s.Hostname,
                ID = s.GetHashCode(),
            });

            ViewBag.Title = "Web Console";
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
                   var origin = (await IW4MAdmin.ApplicationManager.GetInstance().GetClientService().GetUnique(0)).AsPlayer();
#endif

            var server = IW4MAdmin.ApplicationManager.GetInstance().Servers.First(s => s.GetHashCode() == serverId);
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
