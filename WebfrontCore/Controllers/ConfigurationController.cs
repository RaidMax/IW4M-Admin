using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Configuration;
using System.Linq;
using System.Threading.Tasks;
using WebfrontCore.ViewModels;

namespace WebfrontCore.Controllers
{
    [Authorize]
    public class ConfigurationController : BaseController
    {
        public IActionResult Edit()
        {
            if (Client.Level != SharedLibraryCore.Database.Models.EFClient.Permission.Owner)
            {
                return Unauthorized();
            }

            return View("Index", Manager.GetApplicationSettings().Configuration());
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ApplicationConfiguration newConfiguration, bool addNewServer = false, bool shouldSave = false)
        {
            if (Client.Level != SharedLibraryCore.Database.Models.EFClient.Permission.Owner)
            {
                return Unauthorized();
            }

            if (shouldSave)
            {
                var currentConfiguration = Manager.GetApplicationSettings().Configuration();

                var newConfigurationProperties = newConfiguration.GetType().GetProperties();
                foreach (var property in currentConfiguration.GetType().GetProperties())
                {
                    var newProp = newConfigurationProperties.First(_prop => _prop.Name == property.Name);
                    var newPropValue = newProp.GetValue(newConfiguration);

                    if (newPropValue != null && newProp.CanWrite)
                    {
                        property.SetValue(currentConfiguration, newPropValue);
                    }
                }

                await Manager.GetApplicationSettings().Save();
            }

            if (addNewServer)
            {
                newConfiguration.Servers.Add(new ServerConfiguration());
            }

         
            return View("Index", newConfiguration);
        }

        public IActionResult GetNewListItem(string propertyName, int itemCount)
        {
            if (Client.Level != SharedLibraryCore.Database.Models.EFClient.Permission.Owner)
            {
                return Unauthorized();
            }

            var configInfo = new ConfigurationInfo()
            {
                NewItemCount = itemCount,
                PropertyName = propertyName
            };

            return PartialView("_ListItem", configInfo);
        }
    }
}