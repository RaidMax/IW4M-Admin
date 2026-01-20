using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System.Security.Claims;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Helpers;
using WebfrontCore.Components.Features.Auth.Models;

namespace WebfrontCore.Core.Auth;

public class AccountController(IManager manager) : BaseController(manager)
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
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, privilegedClient.Name),
                    new Claim(ClaimTypes.Role, privilegedClient.Level.ToString()),
                    new Claim(ClaimTypes.Sid, privilegedClient.ClientId.ToString()),
                    new Claim(ClaimTypes.PrimarySid, privilegedClient.NetworkId.ToString("X")),
                    new Claim(ClaimTypes.PrimaryGroupSid, privilegedClient.GameName.ToString())
                };

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
                    Identifier = HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out Microsoft.Extensions.Primitives.StringValues value)
                        ? value.ToString()
                        : HttpContext.Connection.RemoteIpAddress?.ToString()
                });

                return Ok(Localization["WEBFRONT_ACTION_LOGIN_SUCCESS"].FormatExt(privilegedClient.CleanedName));
            }
        }

        catch (Exception)
        {
            return Unauthorized(Localization["WEBFRONT_ACTION_LOGIN_ERROR"]);
        }

        return Unauthorized(Localization["WEBFRONT_ACTION_LOGIN_ERROR"]);
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
