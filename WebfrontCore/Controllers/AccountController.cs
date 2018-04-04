using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace WebfrontCore.Controllers
{
    public class AccountController : BaseController
    {
        [HttpGet]
        public async Task<IActionResult> Login(int userId, string password)
        {
            if (userId == 0 || string.IsNullOrEmpty(password))
            {
                return Unauthorized();
            }

            var client = IW4MAdmin.Program.ServerManager.PrivilegedClients[userId];
            string[] hashedPassword = await Task.FromResult(SharedLibrary.Helpers.Hashing.Hash(password, client.PasswordSalt));

            if (hashedPassword[0] == client.Password)
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, client.Name),
                    new Claim(ClaimTypes.Role, client.Level.ToString()),
                    new Claim(ClaimTypes.Sid, client.ClientId.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, "login");
                var claimsPrinciple = new ClaimsPrincipal(claimsIdentity);
                await HttpContext.Authentication.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrinciple);

                return Ok();
            }

            return Unauthorized();
        }
    }
}
