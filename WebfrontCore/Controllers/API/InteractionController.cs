using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Controllers.API.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Controllers.API
{
    /// <summary>
    /// Plugin-provided "interactions" — named UI fragments (forms, action buttons, embedded HTML)
    /// that plugins register with the webfront. Each interaction is rendered on demand by name.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Interactions")]
    [Produces("application/json")]
    public class InteractionController(IManager manager, IWebfrontDataService dataService)
        : BaseController(manager)
    {
        /// <remarks>
        /// Renders the interaction registered under <paramref name="interactionName"/>. Any
        /// additional query string parameters are passed through to the interaction as a meta
        /// dictionary, letting the plugin supply per-invocation context (e.g. <c>clientId</c>,
        /// <c>serverId</c>). Returns 404 if no plugin has registered the requested name.
        /// </remarks>
        /// <param name="interactionName">Name of the plugin-registered interaction.</param>
        /// <param name="token">Request cancellation token.</param>
        /// <response code="200">Rendered interaction returned.</response>
        /// <response code="401">Caller is not authorised to view this interaction.</response>
        /// <response code="404">No interaction is registered under that name.</response>
        [HttpGet("{interactionName}")]
        [ProducesResponseType<InteractionResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
