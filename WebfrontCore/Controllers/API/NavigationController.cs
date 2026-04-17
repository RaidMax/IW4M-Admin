using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;
using WebfrontCore.Components.UI.Navigation.Models;

namespace WebfrontCore.Controllers.API
{
    /// <summary>
    /// Webfront navigation data — the entries shown in the top bar and side menu,
    /// filtered to pages the caller is allowed to see.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Navigation")]
    [Produces("application/json")]
    public class NavigationController(IManager manager, IWebfrontDataService dataService) : BaseController(manager)
    {
        /// <remarks>
        /// Returns the navigation tree for the webfront, including plugin-contributed pages
        /// and any entries gated on the caller's permission level. Used by the shell to
        /// render menus without hard-coding the page list on the client.
        /// </remarks>
        /// <response code="200">Navigation data returned.</response>
        [HttpGet]
        [ProducesResponseType<NavigationInfo>(StatusCodes.Status200OK)]
        public async Task<ActionResult<NavigationInfo>> GetNavigationData()
        {
            var navData = await dataService.GetNavigationDataAsync();
            return Ok(navData);
        }
    }
}
