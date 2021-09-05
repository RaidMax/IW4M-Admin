﻿using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Data.Context;
using Data.Models;

namespace SharedLibraryCore
{
    public class BaseController : Controller
    {
        /// <summary>
        /// life span in months
        /// </summary>
        private const int COOKIE_LIFESPAN = 3;

        public IManager Manager { get; private set; }
        protected readonly DatabaseContext Context;
        protected bool Authorized { get; set; }
        protected Localization.TranslationLookup Localization { get; private set; }
        protected EFClient Client { get; private set; }
        private static readonly byte[] LocalHost = { 127, 0, 0, 1 };
        private static string SocialLink;
        private static string SocialTitle;
        protected List<Page> Pages;

        public BaseController(IManager manager)
        {
            Manager = manager;

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
            ViewBag.IsFluid = false;
            ViewBag.EnableColorCodes = Manager.GetApplicationSettings().Configuration().EnableColorCodes;

            Client ??= new EFClient()
            {
                ClientId = -1,
                Level = EFClient.Permission.Banned,
                CurrentAlias = new EFAlias() { Name = "Webfront Guest" }
            };
        }

        protected async Task SignInAsync(ClaimsPrincipal claimsPrinciple)
        {
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrinciple, new AuthenticationProperties()
            {
                AllowRefresh = true,
                ExpiresUtc = DateTime.UtcNow.AddMonths(COOKIE_LIFESPAN),
                IsPersistent = true,
                IssuedUtc = DateTime.UtcNow
            });
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!HttpContext.Connection.RemoteIpAddress.GetAddressBytes().SequenceEqual(LocalHost))
            {
                try
                {
                    int clientId = Convert.ToInt32(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Sid)?.Value ?? "-1");

                    if (clientId > 0)
                    {
                        Client.ClientId = clientId;
                        Client.NetworkId = clientId == 1 ? 0 : User.Claims.First(_claim => _claim.Type == ClaimTypes.PrimarySid).Value.ConvertGuidToLong(System.Globalization.NumberStyles.HexNumber);
                        Client.Level = (EFClient.Permission)Enum.Parse(typeof(EFClient.Permission), User.Claims.First(c => c.Type == ClaimTypes.Role).Value);
                        Client.CurrentAlias = new EFAlias() { Name = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value };
                        Authorized = Client.ClientId >= 0;
                    }
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
            else if (!HttpContext.Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                Client.ClientId = 1;
                Client.Level = EFClient.Permission.Console;
                Client.CurrentAlias = new EFAlias() { Name = "IW4MAdmin" };
                Authorized = true;
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, Client.CurrentAlias.Name),
                   new Claim(ClaimTypes.Role, Client.Level.ToString()),
                   new Claim(ClaimTypes.Sid, Client.ClientId.ToString()),
                   new Claim(ClaimTypes.PrimarySid, Client.NetworkId.ToString("X"))
                };
                var claimsIdentity = new ClaimsIdentity(claims, "login");
                SignInAsync(new ClaimsPrincipal(claimsIdentity)).Wait();
            }

            ViewBag.Authorized = Authorized;
            ViewBag.Url = Manager.GetApplicationSettings().Configuration().WebfrontUrl;
            ViewBag.User = Client;
            ViewBag.Version = Manager.Version;
            ViewBag.SocialLink = SocialLink ?? "";
            ViewBag.SocialTitle = SocialTitle;
            ViewBag.Pages = Pages;
            ViewBag.Localization = Utilities.CurrentLocalization.LocalizationIndex;
            ViewBag.CustomBranding = Manager.GetApplicationSettings().Configuration().WebfrontCustomBranding ?? "IW4MAdmin";
            ViewBag.EnableColorCodes = Manager.GetApplicationSettings().Configuration().EnableColorCodes;
            ViewBag.EnablePrivilegedUserPrivacy = Manager.GetApplicationSettings().Configuration().EnablePrivilegedUserPrivacy;

            base.OnActionExecuting(context);
        }
    }
}
