using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.OpenApi;
using WebfrontCore.Core.Services;
using WebfrontCore.Controllers.API.Models;

namespace WebfrontCore.Controllers.API
{
    /// <summary>
    /// Execute IW4MAdmin commands against a specific game server from the webfront console.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Console")]
    [Produces("application/json")]
    [TagDescription("Execute IW4MAdmin commands against a specific game server from the webfront console.")]
    public class ConsoleController(IManager manager, IWebfrontDataService dataService) : BaseController(manager)
    {
        /// <remarks>
        /// Runs an IW4MAdmin command (built-in or plugin-registered) against the target server
        /// as the authenticated client. The command text may be prefixed with the configured
        /// command prefix (e.g. <c>!help</c>) or supplied without it. The response contains
        /// the lines the command would have printed in-game.
        /// </remarks>
        /// <param name="request">Target server id and command text.</param>
        /// <response code="200">Command executed — response lines returned.</response>
        /// <response code="403">Caller lacks <c>Permissions.ConsolePage.Read</c> or the specific command's permission.</response>
        [HttpPost("execute")]
        [Authorize(Policy = "Permissions.ConsolePage.Read")]
        [ProducesResponseType<IEnumerable<CommandResponseInfo>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IEnumerable<CommandResponseInfo>>> ExecuteCommand([FromBody] CommandRequest request)
        {
            var response = await dataService.ExecuteCommandAsync(request.ServerId, request.Command);
            return Ok(response);
        }
    }
}
