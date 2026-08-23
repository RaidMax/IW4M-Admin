using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using IW4MAdmin.Plugins.LiveRadar.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.LiveRadar.Web.Controllers
{
    public class RadarController(
        IManager manager,
        LiveRadarConfiguration config,
        IHttpClientFactory httpClientFactory) : BaseController(manager)
    {
        private readonly IManager _manager = manager;

        [HttpGet]
        [Route("Radar/{serverId}/Map")]
        public async Task<IActionResult> Map(string serverId = null)
        {
            var server = serverId == null
                ? _manager.GetServers().FirstOrDefault()
                : _manager.GetServers().FirstOrDefault(server => server.ToString() == serverId);

            if (server == null)
            {
                return NotFound();
            }

            if ((int)server.GameCode == 15)
            {
                return await GameJson(server, "livemap");
            }

            var map = config.Maps.FirstOrDefault(map => map.Name == server.CurrentMap.Name);
            if (map == null)
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity);
            }

            map.Alias = server.CurrentMap.Alias;
            return Json(map);
        }

        [HttpGet]
        [Route("Radar/{serverId}/Data")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Data(string serverId = null)
        {
            var server = serverId == null
                ? _manager.GetServers().FirstOrDefault()
                : _manager.GetServers().FirstOrDefault(server => server.ToString() == serverId);

            if (server == null)
            {
                return NotFound();
            }

            if ((int)server.GameCode == 15)
            {
                return await GameJson(server, "liveradar");
            }

            var radarInfo = server.GetClientsAsList()
                .Select(client => client.GetAdditionalProperty<RadarDto>("LiveRadar")).ToList();
            return Json(radarInfo);
        }

        [HttpGet]
        [Route("Radar/{serverId}/Tile/{zoom:int}/{tileX:int}/{tileY:int}.png")]
        public async Task<IActionResult> Tile(string serverId, int zoom, int tileX, int tileY)
        {
            var server = _manager.GetServers().FirstOrDefault(item => item.ToString() == serverId);
            if (server == null || (int)server.GameCode != 15)
            {
                return NotFound();
            }

            if (zoom is < 0 or > 4 || tileX is < -128 or > 128 || tileY is < -128 or > 128)
            {
                return BadRequest();
            }

            try
            {
                var dashboardUrl = GetConfigurationValue<Uri>(server, "LiveRadarUrl") ??
                                   new UriBuilder("http", server.ListenAddress, 8080).Uri;
                var tileUrl = new Uri(EnsureTrailingSlash(dashboardUrl), $"map/{zoom}/{tileX}/{tileY}.png");
                using var request = new HttpRequestMessage(HttpMethod.Get, tileUrl);

                var tokenName = GetConfigurationValue<string>(server, "LiveRadarTokenName");
                var secretFile = GetConfigurationValue<string>(server, "LiveRadarTokenSecretFile");
                if (!string.IsNullOrWhiteSpace(tokenName) && !string.IsNullOrWhiteSpace(secretFile))
                {
                    request.Headers.TryAddWithoutValidation("X-SDTD-API-TOKENNAME", tokenName);
                    request.Headers.TryAddWithoutValidation("X-SDTD-API-SECRET",
                        (await System.IO.File.ReadAllTextAsync(secretFile, HttpContext.RequestAborted)).Trim());
                }

                var client = httpClientFactory.CreateClient("LiveRadar7DTD");
                using var response = await client.SendAsync(request, HttpContext.RequestAborted);
                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode);
                }

                var content = await response.Content.ReadAsByteArrayAsync(HttpContext.RequestAborted);
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
                Response.Headers.CacheControl = "private, max-age=86400, stale-while-revalidate=604800";
                return File(content, contentType);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
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

        private async Task<IActionResult> GameJson(Server server, string command)
        {
            try
            {
                var response = await server.ExecuteCommandAsync(command, HttpContext.RequestAborted);
                var payload = string.Join(string.Empty, response).Trim().TrimEnd('\0');
                using var _ = JsonDocument.Parse(payload);
                return new ContentResult
                {
                    Content = payload,
                    ContentType = "application/json",
                    StatusCode = StatusCodes.Status200OK
                };
            }
            catch (JsonException)
            {
                return StatusCode(StatusCodes.Status502BadGateway);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status502BadGateway);
            }
        }

        private static T GetConfigurationValue<T>(Server server, string propertyName) where T : class =>
            server.ServerConfig.GetType().GetProperty(propertyName)?.GetValue(server.ServerConfig) as T;

        private static Uri EnsureTrailingSlash(Uri uri) =>
            uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");
    }
}
