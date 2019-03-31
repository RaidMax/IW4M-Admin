using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace WebfrontCore.Controllers
{
    public class ConfigurationController : BaseController
    {
        public IActionResult Edit()
        {
            return View("Index", Manager.GetApplicationSettings().Configuration());
        }
    }
}