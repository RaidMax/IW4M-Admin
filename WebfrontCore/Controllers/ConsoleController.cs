using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Linq;
using System.Threading.Tasks;
using Data.Models;

namespace WebfrontCore.Controllers
{
    public class ConsoleController : BaseController
    {
        private readonly ApplicationConfiguration _appconfig;

        public ConsoleController(IManager manager) : base(manager)
        {
            _appconfig = manager.GetApplicationSettings().Configuration();
        }

        public IActionResult Index()
        {
            var activeServers = Manager.GetServers().Select(s => new ServerInfo()
            {
                Name = s.Hostname,
                ID = s.EndPoint,
            });

            ViewBag.Description = Localization["WEFBRONT_DESCRIPTION_CONSOLE"];
            ViewBag.Title = Localization["WEBFRONT_CONSOLE_TITLE"];
            ViewBag.Keywords = Localization["WEBFRONT_KEYWORDS_CONSOLE"];

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
                Data = command.StartsWith(_appconfig.CommandPrefix) ||
                       command.StartsWith(_appconfig.BroadcastCommandPrefix)
                    ? command
                    : $"{_appconfig.CommandPrefix}{command}",
                Origin = client,
                Owner = server,
                IsRemote = true
            };

            Manager.AddEvent(remoteEvent);
            CommandResponseInfo[] response = null;

            try
            {
                // wait for the event to process
                var completedEvent =
                    await remoteEvent.WaitAsync(Utilities.DefaultCommandTimeout, server.Manager.CancellationToken);

                if (completedEvent.FailReason == GameEvent.EventFailReason.Timeout)
                {
                    response = new[]
                    {
                        new CommandResponseInfo()
                        {
                            ClientId = client.ClientId,
                            Response = Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_COMMAND_TIMEOUT"]
                        }
                    };
                }

                else
                {
                    response = completedEvent.Output.Select(output => new CommandResponseInfo()
                    {
                        Response = output,
                        ClientId = client.ClientId
                    }).ToArray();
                }
            }

            catch (System.OperationCanceledException)
            {
                response = new[]
                {
                    new CommandResponseInfo()
                    {
                        ClientId = client.ClientId,
                        Response = Utilities.CurrentLocalization.LocalizationIndex["COMMADS_RESTART_SUCCESS"]
                    }
                };
            }

            return View("_Response", response);
        }
    }
}