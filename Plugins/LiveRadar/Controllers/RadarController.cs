using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using IW4MAdmin.Plugins.LiveRadar.Configuration;
using IW4MAdmin.Plugins.LiveRadar.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Data.Models;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.LiveRadar.Web.Controllers;

public class RadarController(
    IManager manager,
    LiveRadarConfiguration config,
    SevenDaysToDieRadarService sevenDaysRadar,
    ILogger<RadarController> logger) : BaseController(manager)
{
    private readonly IManager _manager = manager;

    [HttpGet]
    [Route("Radar/{serverId}/Map")]
    [EnableRateLimiting("liveRadar")]
    public async Task<IActionResult> Map(string serverId = null)
    {
        var server = FindServer(serverId);
        if (server is null) return NotFound();
        if (server.GameCode == Reference.Game.D7D)
        {
            if (!Authorized) return Unauthorized();
            return await ExecuteSevenDaysRequest(server, () => sevenDaysRadar.GetMapAsync(server,
                HttpContext.RequestAborted));
        }

        var map = config.Maps.FirstOrDefault(item => item.Name == server.CurrentMap.Name);
        if (map is null) return StatusCode(StatusCodes.Status422UnprocessableEntity);
        map.Alias = server.CurrentMap.Alias;
        return Json(map);
    }

    [HttpGet]
    [Route("Radar/{serverId}/Data")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [EnableRateLimiting("liveRadar")]
    public async Task<IActionResult> Data(string serverId = null)
    {
        var server = FindServer(serverId);
        if (server is null) return NotFound();
        if (server.GameCode == Reference.Game.D7D)
        {
            if (!Authorized) return Unauthorized();
            return await ExecuteSevenDaysRequest(server, () => sevenDaysRadar.GetPlayersAsync(server,
                HttpContext.RequestAborted));
        }

        return Json(server.GetClientsAsList()
            .Select(client => client.GetAdditionalProperty<RadarDto>("LiveRadar")).ToList());
    }

    [HttpGet]
    [Route("Radar/{serverId}/Tile/{zoom:int}/{tileX:int}/{tileY:int}.png")]
    [EnableRateLimiting("liveRadar")]
    public async Task<IActionResult> Tile(string serverId, int zoom, int tileX, int tileY)
    {
        var server = FindServer(serverId);
        if (server is null || server.GameCode != Reference.Game.D7D) return NotFound();
        if (!Authorized) return Unauthorized();
        if (zoom is < 0 or > 4 || tileX is < -128 or > 128 || tileY is < -128 or > 128) return BadRequest();

        try
        {
            var content = await sevenDaysRadar.GetTileAsync(server, zoom, tileX, tileY,
                HttpContext.RequestAborted);
            Response.Headers.CacheControl = "private, max-age=86400, stale-while-revalidate=604800";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(content, "image/png");
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Invalid 7DTD external web configuration for {Server}", server);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvalidDataException)
        {
            return StatusCode(StatusCodes.Status502BadGateway);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          System.Security.SecurityException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway);
        }
        catch (TaskCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout);
        }
    }

    private Server FindServer(string serverId) => serverId is null
        ? _manager.GetServers().FirstOrDefault()
        : _manager.GetServers().FirstOrDefault(server => server.ToString() == serverId);

    private async Task<IActionResult> ExecuteSevenDaysRequest(Server server, Func<Task<object>> request)
    {
        try
        {
            return Json(await request());
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not retrieve 7DTD Live Radar data from {Server}", server);
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }
}
