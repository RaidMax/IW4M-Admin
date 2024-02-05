using System;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Configuration.Attributes;
using SharedLibraryCore.Configuration.Validation;
using SharedLibraryCore.Interfaces;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebfrontCore.ViewModels;

namespace WebfrontCore.Controllers
{
    [Authorize]
    public class ConfigurationController : BaseController
    {
        private readonly ApplicationConfigurationValidator _validator;

        public ConfigurationController(IManager manager) : base(manager)
        {
            _validator = new ApplicationConfigurationValidator();
        }

        /// <summary>
        /// Endpoint to get the current configuration view
        /// </summary>
        /// <returns></returns>
        public IActionResult Edit()
        {
            if (Client.Level < SharedLibraryCore.Database.Models.EFClient.Permission.Owner)
            {
                return Unauthorized();
            }

            return RedirectToAction("Files");
        }

        public async Task<IActionResult> Files()
        {
            if (Client.Level < SharedLibraryCore.Database.Models.EFClient.Permission.Owner)
            {
                return Unauthorized();
            }

            try
            {
                // todo: move this into a service a some point
                var model = await Task.WhenAll(System.IO.Directory
                    .GetFiles(System.IO.Path.Join(Utilities.OperatingDirectory, "Configuration"))
                    .Where(file => file.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
                    .Select(async fileName => new ConfigurationFileInfo
                    {
                        FileName = fileName.Split(System.IO.Path.DirectorySeparatorChar).Last(),
                        FileContent = await System.IO.File.ReadAllTextAsync(fileName)
                    }));

                return View(model);
            }
            catch (Exception ex)
            {
                return Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPatch("{Controller}/File/{fileName}")]
        public async Task<IActionResult> PatchFiles([FromRoute] string fileName)
        {
            if (Client.Level < SharedLibraryCore.Database.Models.EFClient.Permission.Owner)
            {
                return Unauthorized();
            }

            if (!fileName.EndsWith(".json"))
            {
                return BadRequest("File must be of json format.");
            }

            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var content = await reader.ReadToEndAsync();

            if (string.IsNullOrEmpty(content))
            {
                return BadRequest("File content cannot be empty");
            }

            try
            {
                var jsonDocument = JsonDocument.Parse(content);
            }
            catch (JsonException ex)
            {
                return BadRequest($"{fileName}: {ex.Message}");
            }

            var path = Path.Join(Utilities.OperatingDirectory, "Configuration",
                fileName.Replace($"{Path.DirectorySeparatorChar}", ""));

            // todo: move into a service at some point
            if (!System.IO.File.Exists(path))
            {
                return BadRequest($"{fileName} does not exist");
            }

            try
            {
                await System.IO.File.WriteAllTextAsync(path, content);
            }
            catch (Exception ex)
            {
                return Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }

            return NoContent();
        }

        /// <summary>
        /// Endpoint for the save action
        /// </summary>
        /// <param name="newConfiguration">bound configuration</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Save(ApplicationConfiguration newConfiguration)
        {
            // todo: make this authorization middleware instead of these checks
            if (Client.Level < SharedLibraryCore.Database.Models.EFClient.Permission.Owner)
            {
                return Unauthorized();
            }

            CleanConfiguration(newConfiguration);
            var validationResult = _validator.Validate(newConfiguration);

            if (validationResult.IsValid)
            {
                var currentConfiguration = Manager.GetApplicationSettings().Configuration();
                CopyConfiguration(newConfiguration, currentConfiguration);
                await Manager.GetApplicationSettings().Save();
                return Ok(new
                {
                    message = new[] {Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CONFIGURATION_SAVED"]}
                });
            }

            else
            {
                return BadRequest(new
                {
                    message = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CONFIGURATION_SAVE_FAILED"],
                    errors = new[] {validationResult.Errors.Select(_error => _error.ErrorMessage)}
                });
            }
        }

        /// <summary>
        /// Cleans the configuration by removing empty items from from the array
        /// </summary>
        /// <param name="newConfiguration"></param>
        private void CleanConfiguration(ApplicationConfiguration newConfiguration)
        {
            void cleanProperties(object config)
            {
                foreach (var property in config.GetType()
                    .GetProperties().Where(_prop => _prop.CanWrite))
                {
                    var newPropValue = property.GetValue(config);

                    if (newPropValue is ServerConfiguration[] serverConfig)
                    {
                        foreach (var c in serverConfig)
                        {
                            cleanProperties(c);
                        }
                    }

                    // this clears out any null or empty items in the string array
                    if (newPropValue is string[] configArray)
                    {
                        newPropValue = configArray.Where(_str => !string.IsNullOrWhiteSpace(_str)).ToArray();
                    }

                    property.SetValue(config, newPropValue);
                }
            }

            cleanProperties(newConfiguration);
        }

        /// <summary>
        /// Copies required config fields from new to old
        /// </summary>
        /// <param name="newConfiguration">Source config</param>
        /// <param name="oldConfiguration">Destination config</param>
        private void CopyConfiguration(ApplicationConfiguration newConfiguration,
            ApplicationConfiguration oldConfiguration)
        {
            foreach (var property in newConfiguration.GetType()
                .GetProperties().Where(_prop => _prop.CanWrite))
            {
                var newPropValue = property.GetValue(newConfiguration);
                bool isPropNullArray = property.PropertyType.IsArray && newPropValue == null;

                // this prevents us from setting a null array as that could screw reading up
                if (!ShouldIgnoreProperty(property) && !isPropNullArray)
                {
                    property.SetValue(oldConfiguration, newPropValue);
                }
            }
        }

        /// <summary>
        /// Generates the partial view for a new list item
        /// </summary>
        /// <param name="propertyName">name of the property the input element is generated for</param>
        /// <param name="itemCount">how many items exist already</param>
        /// <param name="serverIndex">if it's a server property, which one</param>
        /// <returns></returns>
        public IActionResult GetNewListItem(string propertyName, int itemCount, int serverIndex = -1)
        {
            if (Client.Level < SharedLibraryCore.Database.Models.EFClient.Permission.Owner)
            {
                return Unauthorized();
            }

            // todo: maybe make this cleaner in the future
            if (propertyName.StartsWith("Servers") && serverIndex < 0)
            {
                return PartialView("_ServerItem", new ApplicationConfiguration()
                {
                    Servers = Enumerable.Repeat(new ServerConfiguration(), itemCount + 1).ToArray()
                });
            }

            var model = new BindingHelper()
            {
                Properties = propertyName.Split("."),
                ItemIndex = itemCount,
                ParentItemIndex = serverIndex
            };

            return PartialView("_ListItem", model);
        }

        /// <summary>
        /// Indicates if the property should be ignored when cleaning/copying from one config to another
        /// </summary>
        /// <param name="info">property info of the current property</param>
        /// <returns></returns>
        private bool ShouldIgnoreProperty(PropertyInfo info) => (info.GetCustomAttributes(false)
            .Where(_attr => _attr.GetType() == typeof(ConfigurationIgnore))
            .FirstOrDefault() as ConfigurationIgnore) != null;
    }
}
