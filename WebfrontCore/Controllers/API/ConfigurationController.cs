using Data.Models.Client;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using Microsoft.AspNetCore.Authorization;
using WebfrontCore.Components.Features.Admin.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Authorize(Roles = nameof(EFClient.Permission.Owner))]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public class ConfigurationController(IManager manager, IWebfrontDataService dataService) : BaseController(manager)
    {
        [HttpGet("files")]
        public async Task<ActionResult<IEnumerable<ConfigurationFileInfo>>> GetFiles()
        {
            var files = await dataService.GetConfigurationFilesAsync();
            return Ok(files);
        }

        [HttpPost("files/{fileName}")]
        public async Task<IActionResult> SaveFile([FromRoute] string fileName, [FromBody] ConfigurationFileInfo content)
        {
            if (Client.Level < EFClient.Permission.Owner)
            {
                return Forbid();
            }

            try
            {
                await dataService.SaveConfigurationFileAsync(fileName, content.FileContent);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
