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
        protected bool Authorized { get; set; }
        protected SharedLibraryCore.Localization.Index Localization { get; private set; }
        protected EFClient Client { get; private set; }
        private static byte[] LocalHost = { 127, 0, 0, 1 };
        private static string SocialLink;
        private static string SocialTitle;

        public BaseController()
        {
            if (Manager == null)
                Manager = Program.Manager;

            if (Localization == null)
                Localization = SharedLibraryCore.Utilities.CurrentLocalization.LocalizationIndex;

            if (Manager.GetApplicationSettings().Configuration().EnableSocialLink && SocialLink == null)
            {
                SocialLink = Manager.GetApplicationSettings().Configuration().SocialLinkAddress;
                SocialTitle = Manager.GetApplicationSettings().Configuration().SocialLinkTitle;
            }
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            Client = Client ?? new EFClient()
            {
                ClientId = -1,
                Level = Player.Permission.User,
                CurrentAlias = new EFAlias() { Name = "Web Console Guest" }
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
                    HttpContext.SignOutAsync().Wait(5000);
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
            ViewBag.Url = Manager.GetApplicationSettings().Configuration().WebfrontBindUrl;
            ViewBag.User = Client;
            ViewBag.SocialLink = SocialLink ?? "";
            ViewBag.SocialTitle = SocialTitle;

            base.OnActionExecuting(context);
        }
    }
}
