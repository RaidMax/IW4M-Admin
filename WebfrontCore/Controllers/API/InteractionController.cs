using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class InteractionController : BaseController
    {
        private readonly IInteractionRegistration _interactionRegistration;

        public InteractionController(IManager manager, IInteractionRegistration interactionRegistration) : base(manager)
        {
            _interactionRegistration = interactionRegistration;
        }

        [HttpGet("{interactionName}")]
        public async Task<ActionResult<InteractionResponse>> Render([FromRoute] string interactionName, CancellationToken token)
        {
            var interactionData = (await _interactionRegistration.GetInteractions(interactionName, token: token)).FirstOrDefault();

            if (interactionData is null)
            {
                return NotFound();
            }

            if (Client.Level < interactionData.MinimumPermission)
            {
                return Unauthorized();
            }

            var meta = HttpContext.Request.Query.ToDictionary(key => key.Key, value => value.Value.ToString());
            var result = await _interactionRegistration.ProcessInteraction(interactionName, Client.ClientId, meta: meta, token: token);

            return Ok(new InteractionResponse
            {
                Title = interactionData.Description ?? interactionData.Name,
                Content = result ?? "",
                InteractionType = interactionData.InteractionType.ToString(),
                DisplayMeta = interactionData.DisplayMeta
            });
        }
    }

    public class InteractionResponse
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public string InteractionType { get; set; }
        public string DisplayMeta { get; set; }
    }
}
