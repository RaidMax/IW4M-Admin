using System.Linq;
using System.Threading.Tasks;
using IW4MAdmin.Plugins.Stats;
using IW4MAdmin.Plugins.Stats.Config;
using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.ViewComponents
{
    public class TopPlayersViewComponent : ViewComponent
    {
        private readonly IConfigurationHandler<StatsConfiguration> _configurationHandler;

        public TopPlayersViewComponent(IConfigurationHandler<StatsConfiguration> configurationHandler)
        {
            _configurationHandler = configurationHandler;
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


            ViewBag.UseNewStats = _configurationHandler.Configuration().EnableAdvancedMetrics;
            return View("~/Views/Client/Statistics/Components/TopPlayers/_List.cshtml",
                _configurationHandler.Configuration().EnableAdvancedMetrics
                    ? await Plugin.Manager.GetNewTopStats(offset, count, serverId)
                    : await Plugin.Manager.GetTopStats(offset, count, serverId));
        }
    }
}