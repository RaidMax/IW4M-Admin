using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Controllers.API.Dtos;

namespace WebfrontCore.Controllers.API;

[ApiController]
[Route("api/[controller]")]
public class Info : BaseController
{
    private readonly IServerDataViewer _serverDataViewer;
    
    public Info(IManager manager, IServerDataViewer serverDataViewer) : base(manager)
    {
        _serverDataViewer = serverDataViewer;
    }
    
    [HttpGet]
    public async Task<IActionResult> Get(int period = 24, Reference.Game? game = null, CancellationToken token = default)
    {
        // todo: this is hardcoded currently because the cache doesn't take into consideration the duration, so 
        // we could impact the webfront usage too
        var duration = TimeSpan.FromHours(24);
        var (totalClients, totalRecentClients) =
            await _serverDataViewer.ClientCountsAsync(duration, game, token);
        var (maxConcurrent, maxConcurrentTime) = await _serverDataViewer.MaxConcurrentClientsAsync(overPeriod: duration, token: token);
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
            }
        };

        return Json(response);
    }
}
