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

        public async Task<IViewComponentResult> InvokeAsync(int count, int offset, long? serverId = null)
        {
            if (serverId == 0)
            {
                serverId = null;
            }

            var server = Plugin.ServerManager.GetServers().FirstOrDefault(_server => _server.EndPoint == serverId);

            if (server != null)
            {
                serverId = StatManager.GetIdForServer(server);
            }


            ViewBag.UseNewStats = _config?.EnableAdvancedMetrics ?? true;
            return View("~/Views/Client/Statistics/Components/TopPlayers/_List.cshtml",
                ViewBag.UseNewStats
                    ? await Plugin.Manager.GetNewTopStats(offset, count, serverId)
                    : await Plugin.Manager.GetTopStats(offset, count, serverId));
        }
    }
}
