using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Webfront.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "IW4MAdmin Home";

            return View();
        }

        [Route("server")]
        public ActionResult Server()
        {
            var serverInfo = new SharedLibrary.Dtos.ServerInfo()
            {
            Name ="test server"
            };


            return View("_Server", serverInfo);
        }
    }
}
