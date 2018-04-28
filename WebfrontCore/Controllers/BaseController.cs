using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
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
        protected EFClient Client { get; private set; }
        private static byte[] LocalHost = { 127, 0, 0, 1 };
        private static string DiscordLink;

        public BaseController()
        {
            Manager = Program.Manager;
            if (Manager.GetApplicationSettings().Configuration().EnableDiscordLink)
            {
                string inviteLink = Manager.GetApplicationSettings().Configuration().DiscordInviteCode;
                if (inviteLink != null)
                    DiscordLink = inviteLink.Contains("https") ? inviteLink : $"https://discordapp.com/invite/{inviteLink}";
                else
                    DiscordLink = "";
            }
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            Client = Client ?? new EFClient()
            {
                ClientId = -1
            };

            if (!HttpContext.Connection.RemoteIpAddress.GetAddressBytes().SequenceEqual(LocalHost))
            {
                try
                {
                    Client.ClientId = Convert.ToInt32(base.User.Claims.First(c => c.Type == ClaimTypes.Sid).Value);
                    Client.Level = (Player.Permission)Enum.Parse(typeof(Player.Permission), User.Claims.First(c => c.Type == ClaimTypes.Role).Value);
                    Client.CurrentAlias = new EFAlias() { Name = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value };
                    var stillExists = Manager.GetPrivilegedClients()[Client.ClientId];
                    
                    // this happens if their level has been updated
                    if (stillExists.Level != Client.Level)
                    {
                        Client.Level = stillExists.Level;
                    }
                }

                catch (InvalidOperationException)
                {

                }

                catch (System.Collections.Generic.KeyNotFoundException)
                {
                    // force the "banned" client to be signed out
                    HttpContext.SignOutAsync().Wait();
                }
            }

            else
            {
                Client.ClientId = 1;
                Client.Level = Player.Permission.Console;
                Client.CurrentAlias = new EFAlias() { Name = "IW4MAdmin" };
            }

            Authorized = Client.ClientId >= 0;
            ViewBag.Authorized = Authorized;
            ViewBag.Url = Startup.Configuration["Web:Address"];
            ViewBag.User = Client;
            ViewBag.DiscordLink = DiscordLink;

            base.OnActionExecuting(context);
        }
    }
}
