using IW4MAdmin.Plugins.Stats;
using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace Stats.ViewComponents
{
    public class TopPlayersViewComponent : ViewComponent
    {
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

            return View("_List", await Plugin.Manager.GetTopStats(offset, count, serverId));
        }
    }
}
