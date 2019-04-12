using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Configuration;
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
        public IActionResult Edit(ApplicationConfiguration config)
        {
            return View("Index", Manager.GetApplicationSettings().Configuration());
        }

        public IActionResult GetNewListItem(string propertyName, int itemCount)
        {
            var config = Manager.GetApplicationSettings().Configuration();
            var propertyInfo = config.GetType().GetProperties().First(_prop => _prop.Name == propertyName);

            var configInfo = new ConfigurationInfo()
            {
                Configuration = config,
                PropertyValue = (IList)propertyInfo.GetValue(config),
                PropertyInfo = propertyInfo,
                NewItemCount = itemCount
            };

            return PartialView("_ListItem", configInfo);
        }
    }
}