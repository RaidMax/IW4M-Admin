using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Data.Models;
using Data.Models.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using SharedLibraryCore;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Services;
using SharedLibraryCore.Dtos.Meta.Responses;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using SharedLibraryCore.QueryHelper;
using WebfrontCore.Controllers.API.Models;
using WebfrontCore.Core.Auth;
using WebfrontCore.Core.QueryHelpers.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Controllers.API
{
    /// <summary>
    /// api controller for client operations
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ClientController : BaseController
    {
        private readonly ILogger _logger;
        private readonly ClientService _clientService;
        private readonly IWebfrontDataService _dataService;

        public ClientController(
            ILogger<ClientController> logger,
            ClientService clientService,
            IManager manager,
            IWebfrontDataService dataService)
            : base(manager)
        {
            _dataService = dataService;
            _logger = logger;
            _clientService = clientService;
        }

        [HttpGet("find")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> FindAsync([FromQuery] FindClientRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    Messages = ModelState.Values
                        .SelectMany(value => value.Errors.Select(error => error.ErrorMessage)).ToArray()
                });
            }

            try
            {
                var response = await _dataService.SearchClientsAsync(request);
                return Ok(response);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to retrieve clients with query - {@Request}", request);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ErrorResponse { Messages = [e.Message] });
            }
        }

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
                var results = await _dataService.GetClientsAsync(request);
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
            if (Manager.GetApplicationSettings().Configuration().EnablePrivilegedUserPrivacy && !Authorized)
            {
                return Forbid();
            }

            var adminsDict = await _dataService.GetPrivilegedClientsAsync();
            return Ok(adminsDict);
        }

        [HttpGet("{clientId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetPlayerInfoAsync([FromRoute] int clientId)
        {
            try
            {
                var clientInfo = await _dataService.GetClientInfoAsync(clientId);
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
                var profile = await _dataService.GetClientProfileAsync(clientId, metaFilterType);
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
            var meta = await _dataService.GetClientMetaAsync(clientId, count, offset, startAt, metaType);
            return Ok(meta);
        }

        [HttpPost("{clientId:int}/login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Login([FromRoute] int clientId, [FromBody, Required] PasswordRequest request)
        {
            if (clientId is 0)
            {
                return Unauthorized();
            }

            if (Authorized)
            {
                return Ok();
            }

            try
            {
                var privilegedClient = await _clientService.GetClientForLogin(clientId);
                var loginSuccess = false;

                if (!Authorized)
                {
                    var tokenData = new TokenIdentifier
                    {
                        ClientId = clientId,
                        Token = request.Password
                    };

                    loginSuccess = Manager.TokenAuthenticator.AuthorizeToken(tokenData) ||
                                   (await Task.FromResult(Hashing.Hash(request.Password,
                                       privilegedClient.PasswordSalt)))[0] ==
                                   privilegedClient.Password;
                }

                if (loginSuccess)
                {
                    List<Claim> claims =
                    [
                        new Claim(ClaimTypes.NameIdentifier, privilegedClient.Name),
                        new Claim(ClaimTypes.Role, privilegedClient.Level.ToString()),
                        new Claim(ClaimTypes.Sid, privilegedClient.ClientId.ToString()),
                        new Claim(ClaimTypes.PrimarySid, privilegedClient.NetworkId.ToString("X")),
                        new Claim(ClaimTypes.PrimaryGroupSid, privilegedClient.GameName.ToString())
                    ];

                    var claimsIdentity = new ClaimsIdentity(claims, "login");
                    var claimsPrinciple = new ClaimsPrincipal(claimsIdentity);
                    await SignInAsync(claimsPrinciple);

                    Manager.AddEvent(new GameEvent
                    {
                        Origin = privilegedClient,
                        Type = GameEvent.EventType.Login,
                        Owner = Manager.GetServers().First(),
                        Data = HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var gameStringValues)
                            ? gameStringValues.ToString()
                            : HttpContext.Connection.RemoteIpAddress?.ToString()
                    });

                    Manager.QueueEvent(new LoginEvent
                    {
                        Source = this,
                        LoginSource = LoginEvent.LoginSourceType.Webfront,
                        EntityId = Client.ClientId.ToString(),
                        Identifier =
                            HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var loginStringValues)
                                ? loginStringValues.ToString()
                                : HttpContext.Connection.RemoteIpAddress?.ToString()
                    });

                    return Ok();
                }
            }
            catch (Exception)
            {
                return Unauthorized();
            }

            return Unauthorized();
        }

        [HttpPost("{clientId:int}/logout")]
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

        public class PasswordRequest
        {
            public string Password { get; set; }
        }
    }
}
