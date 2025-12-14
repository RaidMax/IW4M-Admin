using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebfrontCore.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigurationController : BaseController
    {
        public ConfigurationController(IManager manager) : base(manager)
        {
        }

        [HttpGet("files")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<ConfigurationFileInfo>>> GetFiles()
        {
            if (Client.Level < SharedLibraryCore.Database.Models.EFClient.Permission.Owner)
            {
                return Unauthorized();
            }

            try
            {
                var files = await Task.WhenAll(System.IO.Directory
                    .GetFiles(System.IO.Path.Join(Utilities.OperatingDirectory, "Configuration"))
                    .Where(file => file.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
                    .Select(async fileName => new ConfigurationFileInfo
                    {
                        FileName = fileName.Split(System.IO.Path.DirectorySeparatorChar).Last(),
                        FileContent = await System.IO.File.ReadAllTextAsync(fileName)
                    }));
                
                return Ok(files);
            }
            catch (Exception ex)
            {
                return Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("files/{fileName}")]
        [Authorize]
        public async Task<IActionResult> SaveFile([FromRoute] string fileName, [FromBody] ConfigurationFileInfo content)
        {
             if (Client.Level < SharedLibraryCore.Database.Models.EFClient.Permission.Owner)
            {
                return Unauthorized();
            }

            if (!fileName.EndsWith(".json"))
            {
                return BadRequest("File must be of json format.");
            }

            // content.FileContent is the body
            if (string.IsNullOrEmpty(content.FileContent))
            {
                 // Check if raw body?
                 // FromBody binding usually expects JSON. If we send { "FileContent": "..." } it works.
                 return BadRequest("File content cannot be empty");
            }
            
             // Verification it is valid json
            try
            {
                System.Text.Json.JsonDocument.Parse(content.FileContent);
            }
            catch (System.Text.Json.JsonException ex)
            {
                return BadRequest($"{fileName}: {ex.Message}");
            }

            var path = Path.Join(Utilities.OperatingDirectory, "Configuration",
                fileName.Replace($"{Path.DirectorySeparatorChar}", ""));

            if (!System.IO.File.Exists(path))
            {
                return NotFound($"{fileName} does not exist");
            }

            try
            {
                await System.IO.File.WriteAllTextAsync(path, content.FileContent);
            }
            catch (Exception ex)
            {
                return Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }

            return NoContent();
        }
    }
}
