using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using SharedLibraryCore;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Dtos.Meta.Responses;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using WebfrontCore.Core.OpenApi;
using WebfrontCore.Core.QueryHelpers.Models;
using WebfrontCore.Core.Services;
using WebfrontCore.Controllers.API.Models;

namespace WebfrontCore.Controllers.API
{
    /// <summary>
    /// Player (client) lookups, profile and meta data, plus login/logout for the web session cookie.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Clients")]
    [Produces("application/json")]
    [TagDescription("Player (client) lookups, profile and meta data, plus login/logout for the web session cookie.")]
    public class ClientController(
        ILogger<ClientController> logger,
        IManager manager,
        IWebfrontDataService dataService)
        : BaseController(manager)
    {
        private readonly ILogger _logger = logger;

        /// <remarks>
        /// Searches for clients matching the supplied criteria (name, IP, GUID, etc.).
        /// At least one search field must be provided, otherwise a 400 is returned.
        /// </remarks>
        /// <param name="request">Search criteria and pagination.</param>
        /// <response code="200">Matching clients returned.</response>
        /// <response code="400">No criteria supplied, or request validation failed.</response>
        /// <response code="500">Unexpected server error.</response>
        [HttpGet("search")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SearchAsync([FromQuery] ClientResourceRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    Messages = ModelState.Values
                        .SelectMany(value => value.Errors.Select(error => error.ErrorMessage)).ToArray()
                });
            }

            if (!request.HasData)
            {
                return BadRequest(new ErrorResponse
                {
                    Messages = ["You must provide at least 1 search criteria"]
                });
            }

            try
            {
                var results = await dataService.SearchClientsAsync(request);
                return Ok(results);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to search clients with query - {@Request}", request);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ErrorResponse { Messages = [e.Message] });
            }
        }

        /// <remarks>
        /// Returns clients with elevated permission levels (Trusted and above), grouped by permission.
        /// Used to render the "Privileged Clients" page.
        /// </remarks>
        /// <response code="200">Privileged clients returned, grouped by permission level.</response>
        /// <response code="403">Caller lacks <c>Permissions.PrivilegedClientsPage.Read</c>.</response>
        [HttpGet("privileged")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Authorize(Policy = "Permissions.PrivilegedClientsPage.Read")]
        public async Task<IActionResult> GetPrivilegedAsync()
        {
            var adminsDict = await dataService.GetPrivilegedClientsAsync();
            return Ok(adminsDict);
        }

        /// <remarks>
        /// Returns summary information for a single client — current name, level, aliases,
        /// and recent connection details. For full profile data use <c>/api/client/{id}/profile</c>.
        /// </remarks>
        /// <param name="clientId">IW4MAdmin client identifier.</param>
        /// <response code="200">Client info returned.</response>
        /// <response code="400">Invalid client id.</response>
        /// <response code="500">Unexpected server error.</response>
        [HttpGet("{clientId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetPlayerInfoAsync([FromRoute] int clientId)
        {
            try
            {
                var clientInfo = await dataService.GetClientInfoAsync(clientId);
                return Ok(clientInfo);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to retrieve information for Client - {ClientId}", clientId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ErrorResponse { Messages = [e.Message] });
            }
        }

        /// <remarks>
        /// Returns the full player profile — aliases, IP history, meta entries, level, stats summary,
        /// and connection history. Meta entries can be filtered to a single category with <c>metaFilterType</c>.
        /// </remarks>
        /// <param name="clientId">IW4MAdmin client identifier.</param>
        /// <param name="metaFilterType">Optional meta category filter (e.g. <c>Information</c>, <c>QuickMessage</c>).</param>
        /// <response code="200">Profile returned.</response>
        /// <response code="404">Client does not exist.</response>
        [HttpGet("{clientId:int}/profile")]
        [ProducesResponseType<PlayerInfo>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PlayerInfo>> GetProfileAsync([FromRoute] int clientId,
            [FromQuery] MetaType? metaFilterType)
        {
            try
            {
                var profile = await dataService.GetClientProfileAsync(clientId, metaFilterType);
                return Ok(profile);
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        /// <remarks>
        /// Returns a paginated feed of meta entries for a client (penalties, messages, connections,
        /// and plugin-contributed entries). Used to render the profile timeline.
        /// </remarks>
        /// <param name="clientId">IW4MAdmin client identifier.</param>
        /// <param name="count">Page size.</param>
        /// <param name="offset">Pagination offset.</param>
        /// <param name="startAt">Optional Unix timestamp (ms) — return entries older than this.</param>
        /// <param name="metaType">Optional meta category filter.</param>
        /// <param name="token">Request cancellation token.</param>
        /// <response code="200">Meta entries returned.</response>
        [HttpGet("{clientId:int}/meta")]
        [ProducesResponseType<IEnumerable<BaseMetaResponse>>(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<BaseMetaResponse>>> GetMetaAsync([FromRoute] int clientId,
            [FromQuery] int count, [FromQuery] int offset, [FromQuery] long? startAt, [FromQuery] MetaType? metaType,
            CancellationToken token)
        {
            var meta = await dataService.GetClientMetaAsync(new ClientMetaRequest
            {
                ClientId = clientId,
                Count = count,
                Offset = offset,
                StartAt = startAt,
                MetaType = metaType
            });
            return Ok(meta);
        }

        /// <remarks>
        /// Authenticates a client against their webfront password and, on success, issues a session cookie.
        /// If the account has 2FA enabled, the first call returns 401 with <c>requires2FA=true</c>;
        /// resubmit with <c>twoFactorCode</c> populated to complete login.
        /// </remarks>
        /// <param name="clientId">IW4MAdmin client identifier.</param>
        /// <param name="request">Password and optional 2FA code.</param>
        /// <response code="200">Login succeeded — session cookie issued.</response>
        /// <response code="400">Request body missing or invalid.</response>
        /// <response code="401">Invalid credentials, or 2FA code required/invalid.</response>
        /// <response code="500">Unexpected server error.</response>
        [HttpPost("{clientId:int}/login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Login([FromRoute] int clientId, [FromBody, Required] PasswordRequest request)
        {
            if (Authorized)
            {
                return Ok();
            }

            var ip = HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var val)
                ? val.ToString()
                : HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP Unavailable";

            try
            {
                var principal = await dataService.LoginAsync(new ServiceLoginRequest
                {
                    ClientId = clientId,
                    Password = request.Password,
                    TwoFactorCode = request.TwoFactorCode,
                    IpAddress = ip
                });
                await SignInAsync(principal);
                return Ok();
            }
            catch (UnauthorizedAccessException ex) when (ex.Message == "WEBFRONT_LOGIN_ERROR_2FA_REQUIRED")
            {
                return Unauthorized(new { requires2FA = true, message = "WEBFRONT_LOGIN_ERROR_2FA_REQUIRED" });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not login client {ClientId}", clientId);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        /// <remarks>
        /// Signs the caller out of the current session and raises a <c>LogoutEvent</c>
        /// so plugins (e.g. audit) can react. Safe to call when not authenticated — returns 200.
        /// </remarks>
        /// <response code="200">Session cleared.</response>
        /// <response code="500">Unexpected server error.</response>
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Logout()
        {
            if (Authorized)
            {
                Manager.AddEvent(new GameEvent
                {
                    Origin = Client,
                    Type = GameEvent.EventType.Logout,
                    Owner = Manager.Servers.First(),
                    Data = HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var gameStringValues)
                        ? gameStringValues.ToString()
                        : HttpContext.Connection.RemoteIpAddress?.ToString()
                });

                Manager.QueueEvent(new LogoutEvent
                {
                    Source = this,
                    LoginSource = LoginEvent.LoginSourceType.Webfront,
                    EntityId = Client.ClientId.ToString(),
                    Identifier = HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var logoutStringValues)
                        ? logoutStringValues.ToString()
                        : HttpContext.Connection.RemoteIpAddress?.ToString()
                });
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok();
        }
    }
}
