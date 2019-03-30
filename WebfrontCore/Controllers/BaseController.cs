using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SharedLibraryCore;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using WebfrontCore.ViewModels;

namespace WebfrontCore.Controllers
{
    public class BaseController : Controller
    {
        protected IManager Manager;
        protected readonly DatabaseContext Context;
        protected bool Authorized { get; set; }
        protected SharedLibraryCore.Localization.Index Localization { get; private set; }
        protected EFClient Client { get; private set; }
        private static readonly byte[] LocalHost = { 127, 0, 0, 1 };
        private static string SocialLink;
        private static string SocialTitle;
        protected List<Page> Pages;

        public BaseController()
        {
            if (Manager == null)
            {
                Manager = Program.Manager;
            }

            if (Localization == null)
            {
                Localization = Utilities.CurrentLocalization.LocalizationIndex;
            }

            if (Manager.GetApplicationSettings().Configuration().EnableSocialLink && SocialLink == null)
            {
                SocialLink = Manager.GetApplicationSettings().Configuration().SocialLinkAddress;
                SocialTitle = Manager.GetApplicationSettings().Configuration().SocialLinkTitle;
            }

            Pages = Manager.GetPageList().Pages
                .Select(page => new Page
                {
                    Name = page.Key,
                    Location = page.Value
                }).ToList();

            ViewBag.Version = Manager.Version;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            Client = Client ?? new EFClient()
            {
                ClientId = -1,
                Level = EFClient.Permission.User,
                CurrentAlias = new EFAlias() { Name = "Webfront Guest" }
            };

            if (!HttpContext.Connection.RemoteIpAddress.GetAddressBytes().SequenceEqual(LocalHost))
            {
                try
                {
                    Client.ClientId = Convert.ToInt32(base.User.Claims.First(c => c.Type == ClaimTypes.Sid).Value);
                    Client.NetworkId = User.Claims.First(_claim => _claim.Type == ClaimTypes.PrimarySid).Value.ConvertLong();
                    Client.Level = (EFClient.Permission)Enum.Parse(typeof(EFClient.Permission), User.Claims.First(c => c.Type == ClaimTypes.Role).Value);
                    Client.CurrentAlias = new EFAlias() { Name = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value };
                    Authorized = Client.ClientId >= 0;
                }

                catch (InvalidOperationException)
                {

                }

                catch (KeyNotFoundException)
                {
                    // force the "banned" client to be signed out
                    HttpContext.SignOutAsync().Wait(5000);
                }
            }

            // give the local host full access
            else
            {
                Client.ClientId = 1;
                Client.Level = EFClient.Permission.Console;
                Client.CurrentAlias = new EFAlias() { Name = "IW4MAdmin" };
                Authorized = true;
            }

            ViewBag.Authorized = Authorized;
            ViewBag.Url = Manager.GetApplicationSettings().Configuration().WebfrontUrl;
            ViewBag.User = Client;
            ViewBag.SocialLink = SocialLink ?? "";
            ViewBag.SocialTitle = SocialTitle;
            ViewBag.Pages = Pages;

            base.OnActionExecuting(context);
        }
    }
}
