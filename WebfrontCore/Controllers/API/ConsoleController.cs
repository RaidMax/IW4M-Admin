using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;
using WebfrontCore.Controllers.API.Models;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ConsoleController(IManager manager, IWebfrontDataService dataService) : BaseController(manager)
    {
        [HttpPost("execute")]
        [Authorize(Policy = "Permissions.ConsolePage.Read")]
        public async Task<ActionResult<IEnumerable<CommandResponseInfo>>> ExecuteCommand([FromBody] CommandRequest request)
        {
            var response = await dataService.ExecuteCommandAsync(request.ServerId, request.Command);
            return Ok(response);
        }
    }
}
