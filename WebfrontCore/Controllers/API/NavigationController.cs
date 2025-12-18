using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;
using WebfrontCore.Components.UI.Navigation.Models;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class NavigationController : BaseController
    {
        private readonly IWebfrontDataService _dataService;

        public NavigationController(IManager manager, IWebfrontDataService dataService) : base(manager)
        {
            _dataService = dataService;
        }

        [HttpGet]
        public async Task<ActionResult<NavigationInfo>> GetNavigationData()
        {
            var navData = await _dataService.GetNavigationDataAsync();
            return Ok(navData);
        }
    }
}
