using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Helpers;
using WebfrontCore.Components.Features.Auth.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Core.Auth;

public class AccountController(
    IManager manager,
    IWebfrontDataService dataService) : BaseController(manager)
{
    [HttpPost]
    public async Task<IActionResult> Login([FromForm] LoginRequest request)
    {
        if (request.ClientId == 0 || string.IsNullOrEmpty(request.Password))
        {
            return Unauthorized(Localization["WEBFRONT_ACTION_LOGIN_ERROR"]);
        }

        try
        {
            var privilegedClient = await Manager.GetClientService().GetClientForLogin(request.ClientId);
            var loginSuccess = false;

            if (!Authorized && !loginSuccess)
            {
                loginSuccess = Manager.TokenAuthenticator.AuthorizeToken(new TokenIdentifier
                               {
                                   ClientId = request.ClientId,
                                   Token = request.Password
                               }) ||
                               (await Task.FromResult(Hashing.Hash(request.Password,
                                   privilegedClient.PasswordSalt)))[0] ==
                               privilegedClient.Password;
            }

            if (loginSuccess)
            {
                if (!string.IsNullOrEmpty(privilegedClient.TwoFactorSecret))
                {
                    if (string.IsNullOrEmpty(request.TwoFactorCode) || request.TwoFactorCode == "null")
                    {
                        return Unauthorized("2FA_REQUIRED");
                    }

                    if (!await dataService.ValidateTwoFactorCodeAsync(privilegedClient.ClientId, request.TwoFactorCode))
                    {
                        return Unauthorized(Localization["WEBFRONT_ACTION_LOGIN_ERROR"]);
                    }
                }

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, privilegedClient.Name),
                    new(ClaimTypes.Role, privilegedClient.Level.ToString()),
                    new(ClaimTypes.Sid, privilegedClient.ClientId.ToString()),
                    new(ClaimTypes.PrimarySid, privilegedClient.NetworkId.ToString("X")),
                    new(ClaimTypes.PrimaryGroupSid, privilegedClient.GameName.ToString())
                };

                var appConfig = Manager.GetApplicationSettings().Configuration();
                if (appConfig.RequireTwoFactorForPrivilegedClients &&
                    privilegedClient.Level >= Data.Models.Client.EFClient.Permission.Moderator &&
                    string.IsNullOrEmpty(privilegedClient.TwoFactorSecret))
                {
                    claims.Add(new Claim("PendingTwoFactorEnrollment", "true"));
                }

                var claimsIdentity = new ClaimsIdentity(claims, "login");
                var claimsPrinciple = new ClaimsPrincipal(claimsIdentity);
                await SignInAsync(claimsPrinciple);

                Manager.AddEvent(new GameEvent
                {
                    Origin = privilegedClient,
                    Type = GameEvent.EventType.Login,
                    Owner = Manager.Servers.First(),
                    Data = HttpContext.Request.Headers.ContainsKey("X-Forwarded-For")
                        ? HttpContext.Request.Headers["X-Forwarded-For"].ToString()
                        : HttpContext.Connection.RemoteIpAddress?.ToString()
                });

                Manager.QueueEvent(new LoginEvent
                {
                    Source = this,
                    LoginSource = LoginEvent.LoginSourceType.Webfront,
                    EntityId = privilegedClient.ClientId.ToString(),
                    Identifier = HttpContext.Request.Headers.TryGetValue("X-Forwarded-For",
                        out var value)
                        ? value.ToString()
                        : HttpContext.Connection.RemoteIpAddress?.ToString()
                });

                return Ok(claims.Any(c => c.Type == "PendingTwoFactorEnrollment")
                    ? "2FA_ENROLLMENT_REQUIRED"
                    : Localization["WEBFRONT_ACTION_LOGIN_SUCCESS"].FormatExt(privilegedClient.CleanedName));
            }
        }
        catch (Exception)
        {
            return Unauthorized(Localization["WEBFRONT_ACTION_LOGIN_ERROR"]);
        }

        return Unauthorized(Localization["WEBFRONT_ACTION_LOGIN_ERROR"]);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> EnableTwoFactor()
    {
        if (!Authorized) return Unauthorized();

        var setupInfo = await dataService.EnableTwoFactorAsync();
        return Ok(setupInfo);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ConfirmTwoFactor([FromBody] TwoFactorConfirmRequest request)
    {
        if (!Authorized) return Unauthorized();

        var result = await dataService.ConfirmTwoFactorAsync(request.Secret, request.Code);

        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest("Invalid Code");
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> DisableTwoFactor()
    {
        if (!Authorized) return Unauthorized();

        await dataService.DisableTwoFactorAsync();
        return Ok();
    }


    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        if (Authorized)
        {
            Manager.AddEvent(new GameEvent
            {
                Origin = Client,
                Type = GameEvent.EventType.Logout,
                Owner = Manager.Servers.First(),
                Data = HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var value)
                    ? value.ToString()
                    : HttpContext.Connection.RemoteIpAddress?.ToString()
            });

            Manager.QueueEvent(new LogoutEvent
            {
                Source = this,
                LoginSource = LoginEvent.LoginSourceType.Webfront,
                EntityId = Client.ClientId.ToString(),
                Identifier = HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var value1)
                    ? value1.ToString()
                    : HttpContext.Connection.RemoteIpAddress?.ToString()
            });
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("/");
    }
}
