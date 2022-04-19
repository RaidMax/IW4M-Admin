using System.Linq;
using System.Threading.Tasks;
using IW4MAdmin.Plugins.Stats;
using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.AspNetCore.Mvc;
using Stats.Config;

namespace WebfrontCore.ViewComponents
{
    public class TopPlayersViewComponent : ViewComponent
    {
        private readonly StatsConfiguration _config;

        public TopPlayersViewComponent(StatsConfiguration config)
        {
            _config = config;
        }

        public async Task<IViewComponentResult> InvokeAsync(int count, int offset, string serverEndpoint = null)
        {
            var server = Plugin.ServerManager.GetServers()
                .FirstOrDefault(server => server.ToString() == serverEndpoint);

            var serverId = server is null ? (long?)null : StatManager.GetIdForServer(server);

            ViewBag.UseNewStats = _config?.EnableAdvancedMetrics ?? true;
            ViewBag.SelectedServerName = server?.Hostname;
            
            return View("~/Views/Client/Statistics/Components/TopPlayers/_List.cshtml",
                ViewBag.UseNewStats
                    ? await Plugin.Manager.GetNewTopStats(offset, count, serverId)
                    : await Plugin.Manager.GetTopStats(offset, count, serverId));
        }
    }
}
