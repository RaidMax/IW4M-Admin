using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Components.Features.Home.Models;
using WebfrontCore.Components.Features.Console.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class InformationController(IManager manager, IWebfrontDataService dataService) : BaseController(manager)
    {
        [HttpGet("about")]
        public async Task<ActionResult<AboutInfo>> GetAbout()
        {
            return await dataService.GetAboutInfoAsync();
        }

        [HttpGet("help")]
        public async Task<ActionResult<List<CommandGroupInfo>>> GetHelp()
        {
            return await dataService.GetHelpCommandsAsync();
        }

        [HttpGet("stats")]
        public async Task<ActionResult<SystemInfo>> GetSystemInfo()
        {
            return await dataService.GetSystemInfoAsync();
        }
    }
}
