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
using WebfrontCore.Core.QueryHelpers.Models;
using WebfrontCore.Core.Services;
using WebfrontCore.Controllers.API.Models;

namespace WebfrontCore.Controllers.API
{
    /// <summary>
    /// api controller for client operations
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ClientController(
        ILogger<ClientController> logger,
        IManager manager,
        IWebfrontDataService dataService)
        : BaseController(manager)
    {
        private readonly ILogger _logger = logger;

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

        [HttpGet("privileged")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Authorize(Policy = "Permissions.PrivilegedClientsPage.Read")]
        public async Task<IActionResult> GetPrivilegedAsync()
        {
            var adminsDict = await dataService.GetPrivilegedClientsAsync();
            return Ok(adminsDict);
        }

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

        [HttpGet("{clientId:int}/profile")]
        [ProducesResponseType(StatusCodes.Status200OK)]
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

        [HttpGet("{clientId:int}/meta")]
        [ProducesResponseType(StatusCodes.Status200OK)]
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
                    IpAddress = ip
                });
                await SignInAsync(principal);
                return Ok();
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

        [HttpPost("/logout")]
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
                    Owner = Manager.GetServers().First(),
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
