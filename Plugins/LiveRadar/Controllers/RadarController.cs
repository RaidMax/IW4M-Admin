using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using IW4MAdmin.Plugins.LiveRadar.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.LiveRadar.Web.Controllers
{
    public class RadarController(
        IManager manager,
        LiveRadarConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<RadarController> logger) : BaseController(manager)
    {
        private const int MaxTileBytes = 4 * 1024 * 1024;
        private const int MaxSecretBytes = 4 * 1024;
        private readonly IManager _manager = manager;

        [HttpGet]
        [Route("Radar/{serverId}/Map")]
        [EnableRateLimiting("liveRadar")]
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
                if (!Authorized)
                {
                    return Unauthorized();
                }

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
        [EnableRateLimiting("liveRadar")]
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
                if (!Authorized)
                {
                    return Unauthorized();
                }

                return await GameJson(server, "liveradar");
            }

            var radarInfo = server.GetClientsAsList()
                .Select(client => client.GetAdditionalProperty<RadarDto>("LiveRadar")).ToList();
            return Json(radarInfo);
        }

        [HttpGet]
        [Route("Radar/{serverId}/Tile/{zoom:int}/{tileX:int}/{tileY:int}.png")]
        [EnableRateLimiting("liveRadar")]
        public async Task<IActionResult> Tile(string serverId, int zoom, int tileX, int tileY)
        {
            var server = _manager.GetServers().FirstOrDefault(item => item.ToString() == serverId);
            if (server == null || (int)server.GameCode != 15)
            {
                return NotFound();
            }

            if (!Authorized)
            {
                return Unauthorized();
            }

            if (zoom is < 0 or > 4 || tileX is < -128 or > 128 || tileY is < -128 or > 128)
            {
                return BadRequest();
            }

            try
            {
                var dashboardUrl = GetConfigurationValue<Uri>(server, "LiveRadarUrl") ??
                                   new UriBuilder("http", server.ListenAddress, 8080).Uri;
                if (!IsSupportedDashboardUrl(dashboardUrl))
                {
                    return StatusCode(StatusCodes.Status422UnprocessableEntity);
                }

                var tileUrl = new Uri(EnsureTrailingSlash(dashboardUrl), $"map/{zoom}/{tileX}/{tileY}.png");
                using var request = new HttpRequestMessage(HttpMethod.Get, tileUrl);

                var tokenName = GetConfigurationValue<string>(server, "LiveRadarTokenName");
                var secretFile = GetConfigurationValue<string>(server, "LiveRadarTokenSecretFile");
                if (!string.IsNullOrWhiteSpace(tokenName) && !string.IsNullOrWhiteSpace(secretFile))
                {
                    if (tokenName.Length > 256 || tokenName.Contains('\r') || tokenName.Contains('\n'))
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable);
                    }

                    var secretInfo = new FileInfo(secretFile);
                    if (!secretInfo.Exists || secretInfo.Length is <= 0 or > MaxSecretBytes)
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable);
                    }

                    var secret = (await System.IO.File.ReadAllTextAsync(secretFile, HttpContext.RequestAborted)).Trim();
                    if (string.IsNullOrEmpty(secret) || secret.Contains('\r') || secret.Contains('\n'))
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable);
                    }

                    request.Headers.Add("X-SDTD-API-TOKENNAME", tokenName);
                    request.Headers.Add("X-SDTD-API-SECRET", secret);
                }

                var client = httpClientFactory.CreateClient("LiveRadar7DTD");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                    HttpContext.RequestAborted);
                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode);
                }

                if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "image/png",
                        StringComparison.OrdinalIgnoreCase) || response.Content.Headers.ContentLength is > MaxTileBytes)
                {
                    return StatusCode(StatusCodes.Status502BadGateway);
                }

                var content = await ReadBoundedContentAsync(response.Content, MaxTileBytes,
                    HttpContext.RequestAborted);
                Response.Headers.CacheControl = "private, max-age=86400, stale-while-revalidate=604800";
                Response.Headers["X-Content-Type-Options"] = "nosniff";
                return File(content, "image/png");
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
            catch (JsonException exception)
            {
                logger.LogWarning(exception, "Received invalid Live Radar JSON from server {Server}", server);
                return StatusCode(StatusCodes.Status502BadGateway);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not retrieve Live Radar data from server {Server}", server);
                return StatusCode(StatusCodes.Status502BadGateway);
            }
        }

        private static async Task<byte[]> ReadBoundedContentAsync(HttpContent content, int maximumBytes,
            System.Threading.CancellationToken token)
        {
            await using var input = await content.ReadAsStreamAsync(token);
            using var output = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int count;
            while ((count = await input.ReadAsync(buffer, token)) > 0)
            {
                if (output.Length + count > maximumBytes)
                {
                    throw new InvalidDataException("Live Radar tile exceeded the configured size limit");
                }

                await output.WriteAsync(buffer.AsMemory(0, count), token);
            }

            return output.ToArray();
        }

        private static T GetConfigurationValue<T>(Server server, string propertyName) where T : class =>
            server.ServerConfig.GetType().GetProperty(propertyName)?.GetValue(server.ServerConfig) as T;

        private static Uri EnsureTrailingSlash(Uri uri) =>
            uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");

        private static bool IsSupportedDashboardUrl(Uri uri) =>
            uri.IsAbsoluteUri && string.IsNullOrEmpty(uri.UserInfo) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
