using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConsoleController : BaseController
    {
        private readonly IWebfrontDataService _dataService;

        public ConsoleController(IManager manager, IWebfrontDataService dataService) : base(manager)
        {
            _dataService = dataService;
        }

        [HttpPost("execute")]
        [Authorize(Policy = "Permissions.ConsolePage.Read")]
        public async Task<ActionResult<IEnumerable<CommandResponseInfo>>> ExecuteCommand([FromBody] ConsoleCommandRequest request)
        {
            var response = await _dataService.ExecuteCommandAsync(request.ServerId, request.Command);
            return Ok(response);
        }
    }

    public class ConsoleCommandRequest
    {
        public string ServerId { get; set; }
        public string Command { get; set; }
    }
}
