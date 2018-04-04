using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SharedLibrary.Dtos;

namespace WebfrontCore.Controllers
{
    public class HomeController : BaseController
    {
        public IActionResult Index()
        {
            ViewBag.Description = "IW4MAdmin is a complete server administration tool for IW4x.";
            ViewBag.Title = "Server Overview";
            ViewBag.Keywords = "IW4MAdmin, server, administration, IW4x, MW2, Modern Warfare 2";

            return View();
        }

        public IActionResult Error()
        {
            ViewBag.Description = "IW4MAdmin encountered an error";
            ViewBag.Title = "Error!";
            return View();
        }
    }
}
