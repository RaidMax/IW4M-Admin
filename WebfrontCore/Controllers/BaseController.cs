using IW4MAdmin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SharedLibrary;
using SharedLibrary.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.Controllers
{
    public class BaseController : Controller
    {
        protected ApplicationManager Manager;
        protected bool Authorized { get; private set; }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            Manager = IW4MAdmin.Program.ServerManager;
            Authorized = context.HttpContext.Connection.RemoteIpAddress.ToString() == "127.0.0.1" || 
                Manager.AdministratorIPs.Contains(context.HttpContext.Connection.RemoteIpAddress.ToString().ConvertToIP());
            ViewBag.Authorized = Authorized;
            base.OnActionExecuting(context);
        }
    }
}
