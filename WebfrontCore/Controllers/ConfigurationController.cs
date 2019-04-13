using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using WebfrontCore.ViewModels;

namespace WebfrontCore.Controllers
{
    public class ConfigurationController : BaseController
    {
        public IActionResult Edit()
        {
            return View("Index", Manager.GetApplicationSettings().Configuration());
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ApplicationConfiguration newConfiguration)
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
            return View("Index", newConfiguration);
        }

        public IActionResult GetNewListItem(string propertyName, int itemCount)
        {
            var config = Manager.GetApplicationSettings().Configuration();
            var propertyInfo = config.GetType().GetProperties().First(_prop => _prop.Name == propertyName);

            var configInfo = new ConfigurationInfo()
            {
                Configuration = config,
                PropertyValue = (IList)propertyInfo.GetValue(config) ?? new List<string>(),
                PropertyInfo = propertyInfo,
                NewItemCount = itemCount
            };

            return PartialView("_ListItem", configInfo);
        }
    }
}