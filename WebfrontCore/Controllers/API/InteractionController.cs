using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Controllers.API.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class InteractionController(IManager manager, IWebfrontDataService dataService)
        : BaseController(manager)
    {
        [HttpGet("{interactionName}")]
        public async Task<ActionResult<InteractionResponse>> Render([FromRoute] string interactionName, CancellationToken token)
        {
            var meta = HttpContext.Request.Query.ToDictionary(key => key.Key, value => value.Value.ToString());

            try 
            {
                var response = await dataService.GetInteractionAsync(interactionName, meta);

                if (response is null)
                {
                    return NotFound();
                }

                return Ok(response);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
        }
    }


}
