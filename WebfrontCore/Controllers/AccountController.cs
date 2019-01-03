using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System;
using Microsoft.AspNetCore.Authentication;

namespace WebfrontCore.Controllers
{
    public class AccountController : BaseController
    {
        [HttpGet]
        public async Task<IActionResult> LoginAsync(int clientId, string password)
        {
            if (clientId == 0 || string.IsNullOrEmpty(password))
            {
                return Unauthorized();
            }

            try
            {
                var client = Manager.GetPrivilegedClients()[clientId];
                string[] hashedPassword = await Task.FromResult(SharedLibraryCore.Helpers.Hashing.Hash(password, client.PasswordSalt));

                if (hashedPassword[0] == client.Password)
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
                        ExpiresUtc = DateTime.UtcNow.AddDays(30),
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
