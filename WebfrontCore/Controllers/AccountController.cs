using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace WebfrontCore.Controllers
{
    public class AccountController : BaseController
    {
        /// <summary>
        /// life span in months
        /// </summary>
        private const int COOKIE_LIFESPAN = 3;

        [HttpGet]
        public async Task<IActionResult> LoginAsync(int clientId, string password, Microsoft.AspNetCore.Http.HttpContext ctx = null)
        {
            if (clientId == 0 || string.IsNullOrEmpty(password))
            {
                return Unauthorized();
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
                    await (ctx ?? HttpContext).SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrinciple, new AuthenticationProperties()
                    {
                        AllowRefresh = true,
                        ExpiresUtc = DateTime.UtcNow.AddMonths(COOKIE_LIFESPAN),
                        IsPersistent = true,
                        IssuedUtc = DateTime.UtcNow
                    });

                    return Ok();
                }
            }

            catch (Exception)
            {
                return Unauthorized();
            }

            return Unauthorized();
        }

        [HttpGet]
        public async Task<IActionResult> LogoutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}
