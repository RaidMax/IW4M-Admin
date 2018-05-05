using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using WebfrontCore.ViewModels;

namespace WebfrontCore.Controllers
{
    public class ActionController : BaseController
    {
        public IActionResult BanForm()
        {
            var info = new ActionInfo()
            {
                ActionButtonLabel = Localization["WEBFRONT_ACTION_BAN_NAME"],
                Name = "Ban",
                Inputs = new List<InputInfo>()
                {
                    new InputInfo()
                    {
                      Name = "Reason",
                      Label = Localization["WEBFRONT_ACTION_LABEL_REASON"],
                    }
                },
                Action = "BanAsync"
            };

            return View("_ActionForm", info);
        }

        public async Task<IActionResult> BanAsync(int targetId, string Reason)
        {
            var server = Manager.GetServers().First();

            return await Task.FromResult(RedirectToAction("ExecuteAsync", "Console", new
            {
                serverId = server.GetHashCode(),
                command = $"!ban @{targetId} {Reason}"
            }));
        }

        public IActionResult UnbanForm()
        {
            var info = new ActionInfo()
            {
                ActionButtonLabel = Localization["WEBFRONT_ACTION_UNBAN_NAME"],
                Name = "Unban",
                Inputs = new List<InputInfo>()
                {
                    new InputInfo()
                    {
                      Name = "Reason",
                      Label = Localization["WEBFRONT_ACTION_LABEL_REASON"],
                    }
                },
                Action = "UnbanAsync"
            };

            return View("_ActionForm", info);
        }

        public async Task<IActionResult> UnbanAsync(int targetId, string Reason)
        {
            var server = Manager.GetServers().First();

            return await Task.FromResult(RedirectToAction("ExecuteAsync", "Console", new
            {
                serverId = server.GetHashCode(),
                command = $"!unban @{targetId} {Reason}"
            }));
        }

        public IActionResult LoginForm()
        {
            var login = new ActionInfo()
            {
                ActionButtonLabel = Localization["WEBFRONT_ACTION_LOGIN_NAME"],
                Name = "Login",
                Inputs = new List<InputInfo>()
                {
                    new InputInfo()
                    {
                        Name = "clientId",
                        Label = Localization["WEBFRONT_ACTION_LABEL_ID"]
                    },
                    new InputInfo()
                    {
                        Name = "Password",
                        Label = Localization["WEBFRONT_ACTION_LABEL_PASSWORD"],
                        Type = "password",
                    }
                },
                Action = "LoginAsync"
            };

            return View("_ActionForm", login);
        }

        public async Task<IActionResult> LoginAsync(int clientId, string password)
        {
            return await Task.FromResult(RedirectToAction("LoginAsync", "Account", new { clientId, password }));
        }
    }
}
