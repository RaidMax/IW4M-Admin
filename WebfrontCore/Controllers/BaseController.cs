using IW4MAdmin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SharedLibrary;
using SharedLibrary.Database.Models;
using System.Collections.Generic;

namespace WebfrontCore.Controllers
{
    public class BaseController : Controller
    {
        protected ApplicationManager Manager;
        protected bool Authorized { get; private set; }
        protected EFClient User { get; private set; }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            Manager = IW4MAdmin.Program.ServerManager;

            User = new EFClient()
            {
                ClientId = -1
            };

            try
            {
                User.ClientId = Manager.PrivilegedClients[context.HttpContext.Connection.RemoteIpAddress.ToString().ConvertToIP()];
            }

            catch (KeyNotFoundException)
            {

            }

            Authorized = context.HttpContext.Connection.RemoteIpAddress.ToString() == "127.0.0.1" ||
                User.ClientId >= 0;
            ViewBag.Authorized = Authorized;
            ViewBag.Url = Startup.Configuration["Web:Address"];
            string inviteLink = Manager.GetApplicationSettings().Configuration().DiscordInviteCode;
            if (inviteLink != null)
                ViewBag.DiscordLink = inviteLink.Contains("https") ? inviteLink : $"https://discordapp.com/invite/{inviteLink}";
            else
                ViewBag.DiscordLink = "";
            base.OnActionExecuting(context);
        }
    }
}
