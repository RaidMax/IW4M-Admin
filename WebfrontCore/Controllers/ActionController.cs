using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using WebfrontCore.ViewModels;
using static SharedLibraryCore.Database.Models.EFClient;

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
                    },
                    new InputInfo()
                    {
                        Name ="Duration",
                        Label=Localization["WEBFRONT_ACTION_LABEL_DURATION"],
                        Type="select",
                        Values = new Dictionary<string, string>()
                        {
                            {"1", $"1 {Localization["GLOBAL_TIME_HOUR"]}" },
                            {"2", $"6  {Localization["GLOBAL_TIME_HOURS"]}" },
                            {"3", $"1  {Localization["GLOBAL_TIME_DAY"]}" },
                            {"4", $"2  {Localization["GLOBAL_TIME_DAYS"]}" },
                            {"5", $"1  {Localization["GLOBAL_TIME_WEEK"]}" },
                            {"6", $"{Localization["WEBFRONT_ACTION_SELECTION_PERMANENT"]}" },
                        }
                    }
                },
                Action = "BanAsync"
            };

            return View("_ActionForm", info);
        }

        public async Task<IActionResult> BanAsync(int targetId, string Reason, int Duration)
        {
            string duration = string.Empty;

            var loc = Utilities.CurrentLocalization.LocalizationIndex;

            switch (Duration)
            {
                case 1:
                    duration = $"1{loc["GLOBAL_TIME_HOURS"][0]}";
                    break;
                case 2:
                    duration = $"6{loc["GLOBAL_TIME_HOURS"][0]}";
                    break;
                case 3:
                    duration = $"1{loc["GLOBAL_TIME_DAYS"][0]}";
                    break;
                case 4:
                    duration = $"2{loc["GLOBAL_TIME_DAYS"][0]}";
                    break;
                case 5:
                    duration = $"1{loc["GLOBAL_TIME_WEEKS"][0]}";
                    break;
            }

            string command = Duration == 6 ?
                $"!ban @{targetId} {Reason}" :
                $"!tempban @{targetId} {duration} {Reason}";

            var server = Manager.GetServers().First();

            return await Task.FromResult(RedirectToAction("ExecuteAsync", "Console", new
            {
                serverId = server.GetHashCode(),
                command
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

        public IActionResult EditForm()
        {
            var info = new ActionInfo()
            {
                ActionButtonLabel = Localization["WEBFRONT_ACTION_LABEL_EDIT"],
                Name = "Edit",
                Inputs = new List<InputInfo>()
                {
                    new InputInfo()
                    {
                        Name ="level",
                        Label=Localization["WEBFRONT_PROFILE_LEVEL"],
                        Type="select",
                        Values = Enum.GetValues(typeof(Permission)).OfType<Permission>()
                            .Where(p => p <= Client.Level)
                            .Where(p => p != Permission.Banned)
                             .Where(p => p != Permission.Flagged)
                            .ToDictionary(p => p.ToString(), p=> p.ToLocalizedLevelName())
                    },
                },
                Action = "EditAsync"
            };

            return View("_ActionForm", info);
        }

        public async Task<IActionResult> EditAsync(int targetId, string level)
        {
            var server = Manager.GetServers().First();

            return await Task.FromResult(RedirectToAction("ExecuteAsync", "Console", new
            {
                serverId = server.GetHashCode(),
                command = $"!setlevel @{targetId} {level}"
            }));
        }
    }
}
