using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Dtos;

namespace WebfrontCore.Controllers
{
    public class HomeController : BaseController
    {
        public IActionResult Index()
        {
            ViewBag.Description = "IW4MAdmin is a complete server administration tool for IW4x.";
            ViewBag.Title = Localization["WEBFRONT_HOME_TITLE"];
            ViewBag.Keywords = "IW4MAdmin, server, administration, IW4x, MW2, Modern Warfare 2";

            return View();
        }

        public IActionResult Error()
        {
            ViewBag.Description = Localization["WEBFRONT_ERROR_DESC"];
            ViewBag.Title = Localization["WEBFRONT_ERROR_TITLE"];
            return View();
        }
    }
}
