using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConsoleController : BaseController
    {
        private readonly IRemoteCommandService _remoteCommandService;
        private readonly ITranslationLookup _translationLookup;

        public ConsoleController(IManager manager, IRemoteCommandService remoteCommandService, ITranslationLookup translationLookup) : base(manager)
        {
            _remoteCommandService = remoteCommandService;
            _translationLookup = translationLookup;
        }

        [HttpPost("execute")]
        [Authorize(Policy = "Permissions.ConsolePage.Read")]
        public async Task<ActionResult<IEnumerable<CommandResponseInfo>>> ExecuteCommand([FromBody] ConsoleCommandRequest request)
        {
             if (Client.ClientId < 1)
            {
                return Ok(new[]
                {
                    new CommandResponseInfo
                    {
                        Response = _translationLookup["SERVER_COMMANDS_INTERCEPTED"]
                    }
                });
            }

            var server = Manager.GetServers().FirstOrDefault(s => s.EndPoint == request.ServerId);
            if (server == null) return NotFound("Server not found");

            var (success, response) = await _remoteCommandService.ExecuteWithResult(Client.ClientId, null, request.Command,
                Enumerable.Empty<string>(), server);
            
             // The original controller returned Ok(response) or StatusCode(400, response).
             // response is List<CommandResponseInfo> (I assume? Let's check ExecuteWithResult return type).
             // Wait, ExecuteWithResult returns (bool, IList<CommandResponseInfo>).
            return success ? Ok(response) : BadRequest(response);
        }
    }

    public class ConsoleCommandRequest
    {
        public long ServerId { get; set; }
        public string Command { get; set; }
    }
}
