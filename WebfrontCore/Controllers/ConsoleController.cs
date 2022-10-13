using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.Controllers
{
    public class ConsoleController : BaseController
    {
        private readonly IRemoteCommandService _remoteCommandService;

        public ConsoleController(IManager manager, IRemoteCommandService remoteCommandService) : base(manager)
        {
            _remoteCommandService = remoteCommandService;
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

        public async Task<IActionResult> Execute(long serverId, string command)
        {
            var server = Manager.GetServers().First(s => s.EndPoint == serverId);
            var response = await _remoteCommandService.Execute(Client.ClientId, null, command, Enumerable.Empty<string>(), server);
            return response.Any() ? StatusCode(400, response) : Ok(response);
        }
    }
}
