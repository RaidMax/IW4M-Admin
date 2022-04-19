using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WebfrontCore.Controllers
{
    public class AccountController : BaseController
    {
        public AccountController(IManager manager) : base(manager)
        {

        }

        [HttpGet]
        public async Task<IActionResult> Login(int clientId, string password)
        {
            if (clientId == 0 || string.IsNullOrEmpty(password))
            {
                return Unauthorized("Invalid credentials");
            }

            try
            {
                var privilegedClient = await Manager.GetClientService().GetClientForLogin(clientId);
                bool loginSuccess = false;
#if DEBUG
                loginSuccess = clientId == 1;
#endif
                if (!Authorized && !loginSuccess)
                {
                    loginSuccess = Manager.TokenAuthenticator.AuthorizeToken(privilegedClient.NetworkId, password) ||
                        (await Task.FromResult(SharedLibraryCore.Helpers.Hashing.Hash(password, privilegedClient.PasswordSalt)))[0] == privilegedClient.Password;
                }

                if (loginSuccess)
                {
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, privilegedClient.Name),
                        new Claim(ClaimTypes.Role, privilegedClient.Level.ToString()),
                        new Claim(ClaimTypes.Sid, privilegedClient.ClientId.ToString()),
                        new Claim(ClaimTypes.PrimarySid, privilegedClient.NetworkId.ToString("X"))
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, "login");
                    var claimsPrinciple = new ClaimsPrincipal(claimsIdentity);
                    await SignInAsync(claimsPrinciple);
                    
                    Manager.AddEvent(new GameEvent()
                    {
                        Origin = privilegedClient,
                        Type = GameEvent.EventType.Login,
                        Owner = Manager.GetServers().First(),
                        Data = HttpContext.Request.Headers.ContainsKey("X-Forwarded-For") 
                            ? HttpContext.Request.Headers["X-Forwarded-For"].ToString() 
                            : HttpContext.Connection.RemoteIpAddress.ToString()
                    });

                    return Ok($"Welcome {privilegedClient.Name}. You are now logged in");
                }
            }

            catch (Exception)
            {
                return Unauthorized("Could not validate credentials");
            }

            return Unauthorized("Invalid credentials");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            if (Authorized)
            {
                Manager.AddEvent(new GameEvent()
                {
                    Origin = Client,
                    Type = GameEvent.EventType.Logout,
                    Owner = Manager.GetServers().First(),
                    Data = HttpContext.Request.Headers.ContainsKey("X-Forwarded-For") 
                        ? HttpContext.Request.Headers["X-Forwarded-For"].ToString() 
                        : HttpContext.Connection.RemoteIpAddress.ToString()
                });
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}
