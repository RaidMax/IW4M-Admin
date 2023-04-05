using System.Linq;
using System.Threading.Tasks;
using IW4MAdmin.Plugins.Stats;
using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Interfaces;
using Stats.Config;

namespace WebfrontCore.ViewComponents
{
    public class TopPlayersViewComponent : ViewComponent
    {
        private readonly StatsConfiguration _config;
        private readonly StatManager _statManager;

        public TopPlayersViewComponent(StatsConfiguration config, StatManager statManager)
        {
            _config = config;
            _statManager = statManager;
        }

        public async Task<IViewComponentResult> InvokeAsync(int count, int offset, string serverEndpoint = null)
        {
            var server = Plugin.ServerManager.GetServers()
                .FirstOrDefault(server => server.Id == serverEndpoint) as IGameServer;

            var serverId = server?.LegacyDatabaseId;

            ViewBag.UseNewStats = _config?.EnableAdvancedMetrics ?? true;
            ViewBag.SelectedServerName = server?.ServerName;
            
            return View("~/Views/Client/Statistics/Components/TopPlayers/_List.cshtml",
                ViewBag.UseNewStats
                    ? await _statManager.GetNewTopStats(offset, count, serverId)
                    : await _statManager.GetTopStats(offset, count, serverId));
        }
    }
}
