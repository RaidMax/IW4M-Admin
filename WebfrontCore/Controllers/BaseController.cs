using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;

namespace WebfrontCore.Controllers
{
    public class BaseController : Controller
    {
        protected IManager Manager;
        protected readonly DatabaseContext Context;
        protected bool Authorized { get; private set; }
        protected EFClient User { get; private set; }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            Manager = Program.Manager;

            User = new EFClient()
            {
                ClientId = -1
            };

            try
            {
                User.ClientId = Convert.ToInt32(base.User.Claims.First(c => c.Type == ClaimTypes.Sid).Value);
                User.Level = (Player.Permission)Enum.Parse(typeof(Player.Permission), base.User.Claims.First(c => c.Type == ClaimTypes.Role).Value);
                User.CurrentAlias = new EFAlias() { Name = base.User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value };
            }

            catch (InvalidOperationException)
            {

            }

            Authorized = User.ClientId >= 0;
            ViewBag.Authorized = Authorized;
            ViewBag.Url = Startup.Configuration["Web:Address"];
            ViewBag.User = User;

            if (Manager.GetApplicationSettings().Configuration().EnableDiscordLink)
            {
                string inviteLink = Manager.GetApplicationSettings().Configuration().DiscordInviteCode;
                if (inviteLink != null)
                    ViewBag.DiscordLink = inviteLink.Contains("https") ? inviteLink : $"https://discordapp.com/invite/{inviteLink}";
                else
                    ViewBag.DiscordLink = "";
            }
            base.OnActionExecuting(context);
        }
    }
}
