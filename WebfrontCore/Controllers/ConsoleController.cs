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
        private readonly ITranslationLookup _translationLookup;

        public ConsoleController(IManager manager, IRemoteCommandService remoteCommandService, ITranslationLookup translationLookup) : base(manager)
        {
            _remoteCommandService = remoteCommandService;
            _translationLookup = translationLookup;
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
            if (Client.ClientId < 1)
            {
                return Ok(new[]
                {
                    new CommandResponseInfo
                    {
                        Response = _translationLookup["SERVER_COMMANDS_INTERCEPTED"]
                    }
                });
            }

            var server = Manager.GetServers().First(s => s.EndPoint == serverId);
            var (success, response) = await _remoteCommandService.ExecuteWithResult(Client.ClientId, null, command,
                Enumerable.Empty<string>(), server);
            return success ? Ok(response) : StatusCode(400, response);
        }
    }
}
