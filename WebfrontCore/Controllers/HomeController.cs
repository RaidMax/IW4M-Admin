using System;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models.Client;
using Data.Models.Client.Stats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static SharedLibraryCore.Server;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace WebfrontCore.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ITranslationLookup _translationLookup;
        private readonly ILogger _logger;
        private readonly IDataValueCache<EFClient, (int, int)> _serverStatsCache;
        private const string ServerStatKey = nameof(ServerStatKey);

        public HomeController(ILogger<HomeController> logger, IManager manager, ITranslationLookup translationLookup,
            IDataValueCache<EFClient, (int, int)> serverStatsCache) : base(manager)
        {
            _logger = logger;
            _translationLookup = translationLookup;
            _serverStatsCache = serverStatsCache;
            
            _serverStatsCache.SetCacheItem(async set =>
            {
                var count = await set.CountAsync();
                var startOfPeriod = DateTime.UtcNow.AddHours(-24);
                var recentCount = await set.CountAsync(client => client.LastConnection >= startOfPeriod);
                return (count, recentCount);
            }, ServerStatKey);
        }

        public async Task<IActionResult> Index(Game? game = null)
        {
            ViewBag.Description = Localization["WEBFRONT_DESCRIPTION_HOME"];
            ViewBag.Title = Localization["WEBFRONT_HOME_TITLE"];
            ViewBag.Keywords = Localization["WEBFRONT_KEWORDS_HOME"];

            var servers = Manager.GetServers().Where(_server => !game.HasValue || _server.GameName == game);
            var (count, recentCount) = await _serverStatsCache.GetCacheItem(ServerStatKey);

            var model = new IW4MAdminInfo()
            {
                TotalAvailableClientSlots = servers.Sum(_server => _server.MaxClients),
                TotalOccupiedClientSlots = servers.SelectMany(_server => _server.GetClientsAsList()).Count(),
                TotalClientCount = count,
                RecentClientCount = recentCount,
                Game = game,
                ActiveServerGames = Manager.GetServers().Select(_server => _server.GameName).Distinct().ToArray()
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
            var excludedAssembly = typeof(BaseController).Assembly;
            var commands = Manager.GetCommands()
                .Where(_cmd => _cmd.Permission <= Client.Level)
                .OrderByDescending(_cmd => _cmd.Permission)
                .GroupBy(_cmd =>
                {
                    // we need the plugin type the command is defined in
                    var pluginType = _cmd.GetType().Assembly.GetTypes().FirstOrDefault(_type =>
                        _type.Assembly != excludedAssembly && typeof(IPlugin).IsAssignableFrom(_type));
                    return pluginType == null ? _translationLookup["WEBFRONT_HELP_COMMAND_NATIVE"] :
                        pluginType.Name == "ScriptPlugin" ? _translationLookup["WEBFRONT_HELP_SCRIPT_PLUGIN"] :
                        Manager.Plugins.First(_plugin => _plugin.GetType().FullName == pluginType.FullName)
                            .Name; // for now we're just returning the name of the plugin, maybe later we'll include more info
                })
                .Select(_grp => (_grp.Key, _grp.AsEnumerable()));

            return View(commands);
        }
    }
}