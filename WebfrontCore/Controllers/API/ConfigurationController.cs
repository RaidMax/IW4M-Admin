using Data.Models.Client;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using Microsoft.AspNetCore.Authorization;
using WebfrontCore.Components.Features.Admin.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Controllers.API
{
    /// <summary>
    /// IW4MAdmin JSON configuration file management. Owner-only — all endpoints
    /// require the caller to be signed in as the server Owner.
    /// </summary>
    [ApiController]
    [Authorize(Roles = nameof(EFClient.Permission.Owner))]
    [Route("api/[controller]")]
    [Tags("Configuration")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public class ConfigurationController(IManager manager, IWebfrontDataService dataService) : BaseController(manager)
    {
        /// <remarks>
        /// Lists the editable IW4MAdmin configuration files (e.g. <c>IW4MAdminSettings.json</c>,
        /// plugin configs) with their current contents.
        /// </remarks>
        /// <response code="200">Configuration files returned.</response>
        [HttpGet("files")]
        [ProducesResponseType<IEnumerable<ConfigurationFileInfo>>(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ConfigurationFileInfo>>> GetFiles()
        {
            var files = await dataService.GetConfigurationFilesAsync();
            return Ok(files);
        }

        /// <remarks>
        /// Overwrites a configuration file with the supplied contents. The file must be one
        /// of the files returned by <c>GET /api/configuration/files</c>. A server restart may be
        /// required for some settings to take effect.
        /// </remarks>
        /// <param name="fileName">Name of the configuration file to overwrite.</param>
        /// <param name="content">Replacement file contents.</param>
        /// <response code="204">File saved.</response>
        /// <response code="400">File contents invalid (e.g. malformed JSON).</response>
        /// <response code="403">Caller is not the Owner.</response>
        /// <response code="404">Target file does not exist in the editable-files list.</response>
        /// <response code="500">Unexpected server error while writing.</response>
        [HttpPost("files/{fileName}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
