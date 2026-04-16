using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;
using WebfrontCore.Components.UI.Navigation.Models;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class NavigationController(IManager manager, IWebfrontDataService dataService) : BaseController(manager)
    {
        [HttpGet]
        public async Task<ActionResult<NavigationInfo>> GetNavigationData()
        {
            var navData = await dataService.GetNavigationDataAsync();
            return Ok(navData);
        }
    }
}
