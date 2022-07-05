using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using SharedLibraryCore.Helpers;

namespace WebfrontCore.Controllers
{
    public class AccountController : BaseController
    {
        public AccountController(IManager manager) : base(manager)
        {

        }

        [HttpGet]
        [Obsolete]
        public async Task<IActionResult> Login(int clientId, string password)
        {
            if (clientId == 0 || string.IsNullOrEmpty(password))
            {
                return Unauthorized(Localization["WEBFRONT_ACTION_LOGIN_ERROR"]);
            }

            try
            {
                var privilegedClient = await Manager.GetClientService().GetClientForLogin(clientId);
                var loginSuccess = false;
                
                if (Utilities.IsDevelopment)
                {
                    loginSuccess = clientId == 1;
                }

                if (!Authorized && !loginSuccess)
                {
                    loginSuccess = Manager.TokenAuthenticator.AuthorizeToken(new TokenIdentifier
                                   {
                                       ClientId = clientId,
                                       Token = password
                                   }) ||
                                   (await Task.FromResult(Hashing.Hash(password, privilegedClient.PasswordSalt)))[0] ==
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
                        Owner = Manager.GetServers().First(),
                        Data = HttpContext.Request.Headers.ContainsKey("X-Forwarded-For") 
                            ? HttpContext.Request.Headers["X-Forwarded-For"].ToString() 
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

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            if (Authorized)
            {
                Manager.AddEvent(new GameEvent
                {
                    Origin = Client,
                    Type = GameEvent.EventType.Logout,
                    Owner = Manager.GetServers().First(),
                    Data = HttpContext.Request.Headers.ContainsKey("X-Forwarded-For") 
                        ? HttpContext.Request.Headers["X-Forwarded-For"].ToString() 
                        : HttpContext.Connection.RemoteIpAddress?.ToString()
                });
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}
