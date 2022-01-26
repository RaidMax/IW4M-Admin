﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Data.Context;
using Data.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Localization;

namespace SharedLibraryCore
{
    public class BaseController : Controller
    {
        /// <summary>
        ///     life span in months
        /// </summary>
        private const int COOKIE_LIFESPAN = 3;

        private static readonly byte[] LocalHost = { 127, 0, 0, 1 };
        private static string SocialLink;
        private static string SocialTitle;
        protected readonly DatabaseContext Context;
        protected List<Page> Pages;

        public BaseController(IManager manager)
        {
            Manager = manager;
            Localization ??= Utilities.CurrentLocalization.LocalizationIndex;
            AppConfig = Manager.GetApplicationSettings().Configuration();

            if (AppConfig.EnableSocialLink && SocialLink == null)
            {
                SocialLink = AppConfig.SocialLinkAddress;
                SocialTitle = AppConfig.SocialLinkTitle;
            }


            Pages = Manager.GetPageList().Pages
                .Select(page => new Page
                {
                    Name = page.Key,
                    Location = page.Value
                }).ToList();

            ViewBag.Version = Manager.Version;
            ViewBag.IsFluid = false;
            ViewBag.EnableColorCodes = AppConfig.EnableColorCodes;
            ViewBag.Language = Utilities.CurrentLocalization.Culture.TwoLetterISOLanguageName;

            Client ??= new EFClient
            {
                ClientId = -1,
                Level = Data.Models.Client.EFClient.Permission.Banned,
                CurrentAlias = new EFAlias { Name = "Webfront Guest" }
            };
        }

        public IManager Manager { get; }
        protected bool Authorized { get; set; }
        protected TranslationLookup Localization { get; }
        protected EFClient Client { get; }
        protected ApplicationConfiguration AppConfig { get; }

        protected async Task SignInAsync(ClaimsPrincipal claimsPrinciple)
        {
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrinciple,
                new AuthenticationProperties
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
                    var clientId =
                        Convert.ToInt32(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Sid)?.Value ?? "-1");

                    if (clientId > 0)
                    {
                        Client.ClientId = clientId;
                        Client.NetworkId = clientId == 1
                            ? 0
                            : User.Claims.First(_claim => _claim.Type == ClaimTypes.PrimarySid).Value
                                .ConvertGuidToLong(NumberStyles.HexNumber);
                        Client.Level = (Data.Models.Client.EFClient.Permission)Enum.Parse(
                            typeof(Data.Models.Client.EFClient.Permission),
                            User.Claims.First(c => c.Type == ClaimTypes.Role).Value);
                        Client.CurrentAlias = new EFAlias
                            { Name = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value };
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
                Client.Level = Data.Models.Client.EFClient.Permission.Console;
                Client.CurrentAlias = new EFAlias { Name = "IW4MAdmin" };
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

            var communityName = AppConfig.CommunityInformation?.Name;
            var shouldUseCommunityName = !string.IsNullOrWhiteSpace(communityName)
                                         && !communityName.Contains("IW4MAdmin")
                                         && AppConfig.CommunityInformation.IsEnabled;

            ViewBag.Authorized = Authorized;
            ViewBag.Url = AppConfig.WebfrontUrl;
            ViewBag.User = Client;
            ViewBag.Version = Manager.Version;
            ViewBag.SocialLink = SocialLink ?? "";
            ViewBag.SocialTitle = SocialTitle;
            ViewBag.Pages = Pages;
            ViewBag.Localization = Utilities.CurrentLocalization.LocalizationIndex;
            ViewBag.CustomBranding = shouldUseCommunityName
                ? communityName
                : AppConfig.WebfrontCustomBranding ?? "IW4MAdmin";
            ViewBag.EnableColorCodes = AppConfig.EnableColorCodes;
            ViewBag.EnablePrivilegedUserPrivacy = AppConfig.EnablePrivilegedUserPrivacy;
            ViewBag.Configuration = AppConfig;
            ViewBag.ScriptInjection = AppConfig.Webfront?.ScriptInjection;

            base.OnActionExecuting(context);
        }
    }
}