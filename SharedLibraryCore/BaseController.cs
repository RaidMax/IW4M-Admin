using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
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
        protected readonly IInteractionRegistration InteractionRegistration;
        protected readonly IAlertManager AlertManager;

        /// <summary>
        ///     life span in months
        /// </summary>
        private const int CookieLifespan = 3;

        private static readonly byte[] LocalHost = { 127, 0, 0, 1 };
        private static string _socialLink;
        private static string _socialTitle;
        
        protected List<Page> Pages;
        protected List<string> PermissionsSet;
        protected bool Authorized { get; set; }
        protected TranslationLookup Localization { get; }
        protected EFClient Client { get; }
        protected ApplicationConfiguration AppConfig { get; }
        
        public IManager Manager { get; }

        public BaseController(IManager manager)
        {
            InteractionRegistration = manager.InteractionRegistration;
            AlertManager = manager.AlertManager;
            Manager = manager;
            Localization = Utilities.CurrentLocalization.LocalizationIndex;
            AppConfig = Manager.GetApplicationSettings().Configuration();

            if (AppConfig.EnableSocialLink && _socialLink == null)
            {
                _socialLink = AppConfig.SocialLinkAddress;
                _socialTitle = AppConfig.SocialLinkTitle;
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

            Client = new EFClient
            {
                ClientId = -1,
                Level = Data.Models.Client.EFClient.Permission.User,
                CurrentAlias = new EFAlias { Name = "Webfront Guest" }
            };
        }
        
        protected async Task SignInAsync(ClaimsPrincipal claimsPrinciple)
        {
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrinciple,
                new AuthenticationProperties
                {
                    AllowRefresh = true,
                    ExpiresUtc = DateTime.UtcNow.AddMonths(CookieLifespan),
                    IsPersistent = true,
                    IssuedUtc = DateTime.UtcNow
                });
        }

        public override async void OnActionExecuting(ActionExecutingContext context)
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
                            : User.Claims.First(claim => claim.Type == ClaimTypes.PrimarySid).Value
                                .ConvertGuidToLong(NumberStyles.HexNumber);
                        Client.Level = (Data.Models.Client.EFClient.Permission)Enum.Parse(
                            typeof(Data.Models.Client.EFClient.Permission),
                            User.Claims.First(c => c.Type == ClaimTypes.Role).Value);
                        Client.CurrentAlias = new EFAlias
                            { Name = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value };
                        Authorized = Client.ClientId >= 0;
                        Client.GameName =
                            Enum.Parse<Reference.Game>(User.Claims
                                .First(claim => claim.Type == ClaimTypes.PrimaryGroupSid).Value);
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
                    new Claim(ClaimTypes.PrimarySid, Client.NetworkId.ToString("X")),
                    new Claim(ClaimTypes.PrimaryGroupSid, Client.GameName.ToString())
                };
                var claimsIdentity = new ClaimsIdentity(claims, "login");
                SignInAsync(new ClaimsPrincipal(claimsIdentity)).Wait();
            }
            
            if (AppConfig.PermissionSets.ContainsKey(Client.Level.ToString()))
            {
                PermissionsSet = AppConfig.PermissionSets[Client.Level.ToString()];
            }

            var communityName = AppConfig.CommunityInformation?.Name;
            var shouldUseCommunityName = !string.IsNullOrWhiteSpace(communityName)
                                         && !communityName.Contains("IW4MAdmin")
                                         && AppConfig.CommunityInformation.IsEnabled;

            ViewBag.Interactions = await InteractionRegistration.GetInteractions("Webfront::Nav");
            ViewBag.Authorized = Authorized;
            ViewBag.Url = AppConfig.WebfrontUrl;
            ViewBag.User = Client;
            ViewBag.Version = Manager.Version;
            ViewBag.SocialLink = _socialLink ?? "";
            ViewBag.SocialTitle = _socialTitle;
            ViewBag.Pages = Pages;
            ViewBag.Localization = Utilities.CurrentLocalization.LocalizationIndex;
            ViewBag.CustomBranding = shouldUseCommunityName
                ? communityName
                : AppConfig.WebfrontCustomBranding ?? "IW4MAdmin";
            ViewBag.EnableColorCodes = AppConfig.EnableColorCodes;
            ViewBag.EnablePrivilegedUserPrivacy = AppConfig.EnablePrivilegedUserPrivacy;
            ViewBag.Configuration = AppConfig;
            ViewBag.ScriptInjection = AppConfig.Webfront?.ScriptInjection;
            ViewBag.CommunityInformation = AppConfig.CommunityInformation;
            ViewBag.ClientCount = Manager.GetServers().Sum(server => server.ClientNum);
            ViewBag.AdminCount = Manager.GetServers().Sum(server =>
                server.GetClientsAsList()
                    .Count(client => client.Level >= Data.Models.Client.EFClient.Permission.Trusted));
            ViewBag.ReportCount = Manager.GetServers().Sum(server =>
                server.Reports.Count(report => DateTime.UtcNow - report.ReportedOn <= TimeSpan.FromHours(24)));
            ViewBag.PermissionsSet = PermissionsSet;
            ViewBag.Alerts = AlertManager.RetrieveAlerts(Client);

            base.OnActionExecuting(context);
        }
    }
}
