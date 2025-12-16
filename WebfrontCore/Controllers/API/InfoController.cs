using System.Diagnostics;
using Data.Models;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Controllers.API.Models;

namespace WebfrontCore.Controllers.API;

[ApiController]
[Route("api/[controller]")]
public class InfoController(IManager manager, IServerDataViewer serverDataViewer) : BaseController(manager)
{
    [HttpGet]
    public async Task<IActionResult> Get(int period = 24, Reference.Game? game = null, CancellationToken token = default)
    {
        // todo: this is hardcoded currently because the cache doesn't take into consideration the duration, so 
        // we could impact the webfront usage too
        var duration = TimeSpan.FromHours(24);
        var (totalClients, totalRecentClients) =
            await serverDataViewer.ClientCountsAsync(duration, game, token);
        var (maxConcurrent, maxConcurrentTime) = await serverDataViewer.MaxConcurrentClientsAsync(overPeriod: duration, token: token);
        var uptime = DateTime.Now - Process.GetCurrentProcess().StartTime;
        var response = new InfoResponse
        {
            TotalTrackedClients = totalClients,
            TotalConnectedClients = Manager.GetActiveClients().Count,
            TotalClientSlots = Manager.GetServers().Sum(server => server.MaxClients),
            MaxConcurrentClients = new MetricSnapshot<int?>
            {
                Value = maxConcurrent, Time = maxConcurrentTime, 
                EndAt = DateTime.UtcNow,
                StartAt = DateTime.UtcNow - duration
            },
            TotalRecentClients = new MetricSnapshot<int>
            {
                Value = totalRecentClients,
                EndAt = DateTime.UtcNow,
                StartAt = DateTime.UtcNow - duration
            },
            Uptime = uptime,
        };

        return Json(response);
    }
}
