using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static SharedLibraryCore.Server;

namespace WebfrontCore.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ITranslationLookup _translationLookup;

        public HomeController(IManager manager, ITranslationLookup translationLookup) : base(manager)
        {
            _translationLookup = translationLookup;
        }

        public async Task<IActionResult> Index(Game? game = null)
        {
            ViewBag.Description = "IW4MAdmin is a complete server administration tool for IW4x.";
            ViewBag.Title = Localization["WEBFRONT_HOME_TITLE"];
            ViewBag.Keywords = "IW4MAdmin, server, administration, IW4x, MW2, Modern Warfare 2";

            var servers = Manager.GetServers().Where(_server => !game.HasValue ? true : _server.GameName == game);

            var model = new IW4MAdminInfo()
            {
                TotalAvailableClientSlots = servers.Sum(_server => _server.MaxClients),
                TotalOccupiedClientSlots = servers.SelectMany(_server => _server.GetClientsAsList()).Count(),
                TotalClientCount = await Manager.GetClientService().GetTotalClientsAsync(),
                RecentClientCount = await Manager.GetClientService().GetRecentClientCount(),
                Game = game,
                ActiveServerGames = Manager.GetServers().Select(_server => _server.GameName).Distinct().ToArray()
            };

            return View(model);
        }

        public IActionResult Error()
        {
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            Manager.GetLogger(0).WriteError($"[Webfront] {exceptionFeature.Error.Message}");
            Manager.GetLogger(0).WriteDebug(exceptionFeature.Path);
            Manager.GetLogger(0).WriteDebug(exceptionFeature.Error.StackTrace);

            ViewBag.Description = Localization["WEBFRONT_ERROR_DESC"];
            ViewBag.Title = Localization["WEBFRONT_ERROR_TITLE"];
            return View(exceptionFeature.Error);
        }

        public IActionResult ResponseStatusCode(int? statusCode = null)
        {
            return View(statusCode);
        }

        public IActionResult Help()
        {
            ViewBag.IsFluid = true;
            ViewBag.Title = Localization["WEBFRONT_NAV_HELP"];

            // we don't need to the name of the shared library assembly
            var excludedAssembly = typeof(BaseController).Assembly;
            var commands = Manager.GetCommands()
                .Where(_cmd => _cmd.Permission <= Client.Level)
                .OrderByDescending(_cmd => _cmd.Permission)
                .GroupBy(_cmd =>
                {
                    // we need the plugin type the command is defined in
                    var pluginType = _cmd.GetType().Assembly.GetTypes().FirstOrDefault(_type => _type.Assembly != excludedAssembly && typeof(IPlugin).IsAssignableFrom(_type));
                    return pluginType == null ?
                        _translationLookup["WEBFRONT_HELP_COMMAND_NATIVE"] :
                        pluginType.Name == "ScriptPlugin" ? _translationLookup["WEBFRONT_HELP_SCRIPT_PLUGIN"] :
                        Manager.Plugins.First(_plugin => _plugin.GetType() == pluginType).Name; // for now we're just returning the name of the plugin, maybe later we'll include more info
                })
                .Select(_grp => (_grp.Key, _grp.AsEnumerable()));

            return View(commands);
        }
    }
}
