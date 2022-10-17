using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Controllers;

public class InteractionController : BaseController
{
    private readonly IInteractionRegistration _interactionRegistration;

    public InteractionController(IManager manager, IInteractionRegistration interactionRegistration) : base(manager)
    {
        _interactionRegistration = interactionRegistration;
    }

    [HttpGet("[controller]/[action]/{interactionName}")]
    public async Task<IActionResult> Render([FromRoute]string interactionName, CancellationToken token)
    {
        var interactionData = (await _interactionRegistration.GetInteractions(interactionName, token: token)).FirstOrDefault();
      
        if (interactionData is null)
        {
            return NotFound();
        }

        ViewBag.Title = interactionData.Description;

        var result = await _interactionRegistration.ProcessInteraction(interactionName, Client.ClientId, token: token);
        
        return interactionData.InteractionType == InteractionType.TemplateContent
            ? View("Render", result ?? "")
            : Ok(result);
    }
}
