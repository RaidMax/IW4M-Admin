using System.Globalization;
using System.Security.Claims;
using Data.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Localization;

namespace SharedLibraryCore;

public class BaseController : Controller
{
    /// <summary>
    ///     life span in months
    /// </summary>
    private const int CookieLifespan = 3;

    private static string _socialLink;
    protected bool Authorized => User.Identity?.IsAuthenticated == true && Client.ClientId >= 0;
    protected TranslationLookup Localization { get; }
    protected EFClient Client { get; }
    protected ApplicationConfiguration AppConfig { get; }

    public IManager Manager { get; }

    public BaseController(IManager manager)
    {
        Manager = manager;
        Localization = Utilities.CurrentLocalization.LocalizationIndex;
        AppConfig = Manager.GetApplicationSettings().Configuration();

        if (AppConfig.EnableSocialLink && _socialLink == null)
        {
            _socialLink = AppConfig.SocialLinkAddress;
        }

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
                Client.Level =
                    Enum.Parse<Data.Models.Client.EFClient.Permission>(User.Claims
                        .First(c => c.Type == ClaimTypes.Role).Value);
                Client.CurrentAlias = new EFAlias
                    { Name = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value };

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

        base.OnActionExecuting(context);
    }
}
