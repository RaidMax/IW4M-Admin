using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Components.Features.Home.Models;
using WebfrontCore.Components.Features.Console.Models;
using WebfrontCore.Core.OpenApi;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Controllers.API
{
    /// <summary>
    /// General information about the IW4MAdmin instance — About page content,
    /// command help index, and runtime/system stats.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Information")]
    [Produces("application/json")]
    [TagDescription(
        "General information about the IW4MAdmin instance — About page content, command help index, and runtime/system stats.")]
    public class InformationController(IManager manager, IWebfrontDataService dataService) : BaseController(manager)
    {
        /// <remarks>
        /// Returns the content shown on the About page — branding, version, community
        /// description, social links, and any Owner-configured "about this server" text.
        /// </remarks>
        /// <response code="200">About info returned.</response>
        [HttpGet("about")]
        [ProducesResponseType<AboutInfo>(StatusCodes.Status200OK)]
        public async Task<ActionResult<AboutInfo>> GetAbout()
        {
            return await dataService.GetAboutInfoAsync();
        }

        /// <remarks>
        /// Returns the full command reference — every command registered by IW4MAdmin and its
        /// plugins, grouped by category, filtered to the caller's permission level.
        /// Used to render the in-webfront help page.
        /// </remarks>
        /// <response code="200">Command help returned.</response>
        [HttpGet("help")]
        [ProducesResponseType<List<CommandGroupInfo>>(StatusCodes.Status200OK)]
        public async Task<ActionResult<List<CommandGroupInfo>>> GetHelp()
        {
            return await dataService.GetHelpCommandsAsync();
        }

        /// <remarks>
        /// Returns runtime metrics for the IW4MAdmin process — uptime, CPU, memory,
        /// database stats, and per-server connection counters.
        /// </remarks>
        /// <response code="200">System info returned.</response>
        [HttpGet("stats")]
        [ProducesResponseType<SystemInfo>(StatusCodes.Status200OK)]
        public async Task<ActionResult<SystemInfo>> GetSystemInfo()
        {
            return await dataService.GetSystemInfoAsync();
        }
    }
}
