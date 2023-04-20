using System;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace WebfrontCore.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ITranslationLookup _translationLookup;
        private readonly ILogger _logger;
        private readonly IServerDataViewer _serverDataViewer;

        public HomeController(ILogger<HomeController> logger, IManager manager, ITranslationLookup translationLookup,
            IServerDataViewer serverDataViewer) : base(manager)
        {
            _logger = logger;
            _translationLookup = translationLookup;
            _serverDataViewer = serverDataViewer;
        }

        public async Task<IActionResult> Index(Reference.Game? game = null,
            CancellationToken cancellationToken = default)
        {
            ViewBag.Description = Localization["WEBFRONT_DESCRIPTION_HOME"];
            ViewBag.Title = Localization["WEBFRONT_HOME_TITLE"];
            ViewBag.Keywords = Localization["WEBFRONT_KEWORDS_HOME"];

            var servers = Manager.GetServers().Where(server => game is null || server.GameName == (Server.Game?)game)
                .ToList();
            var (clientCount, time) =
                await _serverDataViewer.MaxConcurrentClientsAsync(gameCode: game, token: cancellationToken);
            var (count, recentCount) =
                await _serverDataViewer.ClientCountsAsync(gameCode: game, token: cancellationToken);

            var model = new IW4MAdminInfo
            {
                TotalAvailableClientSlots = servers.Sum(server => server.MaxClients),
                TotalOccupiedClientSlots = servers.SelectMany(server => server.GetClientsAsList()).Count(),
                TotalClientCount = count,
                RecentClientCount = recentCount,
                MaxConcurrentClients = clientCount ?? 0,
                MaxConcurrentClientsTime = time ?? DateTime.UtcNow,
                Game = game,
                ActiveServerGames = Manager.GetServers().Select(server => (Reference.Game)server.GameName).Distinct()
                    .ToArray()
            };

            return View(model);
        }

        public IActionResult Error()
        {
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            _logger.LogError("[Webfront] {path} {message} {@exception}", exceptionFeature.Path,
                exceptionFeature.Error.Message, exceptionFeature.Error);
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
            ViewBag.CommandPrefix = Manager.GetApplicationSettings().Configuration().CommandPrefix;

            // we don't need to the name of the shared library assembly
            var commands = Manager.GetCommands()
                .Where(command => command.Permission <= Client.Level)
                .OrderByDescending(command => command.Permission)
                .GroupBy(command =>
                {
                    if (command.GetType().Name == "ScriptCommand")
                    {
                        return _translationLookup["WEBFRONT_HELP_SCRIPT_PLUGIN"];
                    }

                    var assemblyName = command.GetType().Assembly.GetName().Name;
                    if (assemblyName is "IW4MAdmin" or "SharedLibraryCore")
                    {
                        return _translationLookup["WEBFRONT_HELP_COMMAND_NATIVE"];
                    }

                    var pluginType = command.GetType().Assembly.GetTypes()
                        .FirstOrDefault(type => typeof(IPlugin).IsAssignableFrom(type));
                    return Manager.Plugins.FirstOrDefault(plugin => plugin.GetType() == pluginType)?.Name ??
                           _translationLookup["WEBFRONT_HELP_COMMAND_NATIVE"];
                })
                .Select(group => (group.Key, group.AsEnumerable()));

            return View(commands);
        }
    }
}
