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
        public async Task<IActionResult> LoginAsync(int clientId, string password)
        {
            if (clientId == 0 || string.IsNullOrEmpty(password))
            {
                return Unauthorized();
            }

            try
            {
#if DEBUG == true
                var client = Utilities.IW4MAdminClient();
                bool loginSuccess = true;
#else
                var client = Manager.GetPrivilegedClients()[clientId];
                bool loginSuccess = Manager.TokenAuthenticator.AuthorizeToken(client.NetworkId, password) ||
                    (await Task.FromResult(SharedLibraryCore.Helpers.Hashing.Hash(password, client.PasswordSalt)))[0] == client.Password;
#endif

                if (loginSuccess)
                {
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, client.Name),
                        new Claim(ClaimTypes.Role, client.Level.ToString()),
                        new Claim(ClaimTypes.Sid, client.ClientId.ToString()),
                        new Claim(ClaimTypes.PrimarySid, client.NetworkId.ToString())
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, "login");
                    var claimsPrinciple = new ClaimsPrincipal(claimsIdentity);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrinciple, new AuthenticationProperties()
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
