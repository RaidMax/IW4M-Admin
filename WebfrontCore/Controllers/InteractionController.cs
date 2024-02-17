using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Controllers;

public class InteractionController(IManager manager, IInteractionRegistration interactionRegistration)
    : BaseController(manager)
{
    [HttpGet("[controller]/[action]/{interactionName}")]
    public async Task<IActionResult> Render([FromRoute]string interactionName, CancellationToken token)
    {
        var interactionData = (await interactionRegistration.GetInteractions(interactionName, token: token)).FirstOrDefault();
      
        if (interactionData is null)
        {
            return NotFound();
        }

        if (Client.Level < interactionData.MinimumPermission)
        {
            return Unauthorized();
        }

        ViewBag.Title = interactionData.Description;
        var meta = HttpContext.Request.Query.ToDictionary(key => key.Key, value => value.Value.ToString());
        var result = await interactionRegistration.ProcessInteraction(interactionName, Client.ClientId, meta: meta, token: token);

        if (interactionData.InteractionType == InteractionType.TemplateContent)
        {
            return View("Render", result ?? "");
        }

        return new ContentResult
        {
            Content = result,
            ContentType = interactionData.DisplayMeta ?? "text/html"
        };
    }
}
