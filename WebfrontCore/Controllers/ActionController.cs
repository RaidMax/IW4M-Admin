using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using WebfrontCore.ViewModels;
using static SharedLibraryCore.Database.Models.EFClient;

namespace WebfrontCore.Controllers
{
    public class ActionController : BaseController
    {
        private readonly ApplicationConfiguration _appConfig;
        private readonly string _banCommandName;
        private readonly string _tempbanCommandName;
        private readonly string _unbanCommandName;
        private readonly string _sayCommandName;
        private readonly string _kickCommandName;
        private readonly string _flagCommandName;
        private readonly string _unflagCommandName;
        private readonly string _setLevelCommandName;

        public ActionController(IManager manager, IEnumerable<IManagerCommand> registeredCommands) : base(manager)
        {
            _appConfig = manager.GetApplicationSettings().Configuration();

            foreach (var cmd in registeredCommands)
            {
                var type = cmd.GetType().Name;

                switch (type)
                {
                    case nameof(BanCommand):
                        _banCommandName = cmd.Name;
                        break;
                    case nameof(TempBanCommand):
                        _tempbanCommandName = cmd.Name;
                        break;
                    case nameof(UnbanCommand):
                        _unbanCommandName = cmd.Name;
                        break;
                    case nameof(SayCommand):
                        _sayCommandName = cmd.Name;
                        break;
                    case nameof(KickCommand):
                        _kickCommandName = cmd.Name;
                        break;
                    case nameof(FlagClientCommand):
                        _flagCommandName = cmd.Name;
                        break;
                    case nameof(UnflagClientCommand):
                        _unflagCommandName = cmd.Name;
                        break;
                    case nameof(SetLevelCommand):
                        _setLevelCommandName = cmd.Name;
                        break;
                }
            }
        }

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
                Action = "BanAsync",
                ShouldRefresh = true
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
                $"{_appConfig.CommandPrefix}{_banCommandName} @{targetId} {Reason}" :
                $"{_appConfig.CommandPrefix}{_tempbanCommandName} @{targetId} {duration} {Reason}";

            var server = Manager.GetServers().First();

            return await Task.FromResult(RedirectToAction("ExecuteAsync", "Console", new
            {
                serverId = server.EndPoint,
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
                Action = "UnbanAsync",
                ShouldRefresh = true
            };

            return View("_ActionForm", info);
        }

        public async Task<IActionResult> UnbanAsync(int targetId, string Reason)
        {
            var server = Manager.GetServers().First();

            return await Task.FromResult(RedirectToAction("ExecuteAsync", "Console", new
            {
                serverId = server.EndPoint,
                command = $"{_appConfig.CommandPrefix}{_unbanCommandName} @{targetId} {Reason}"
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
                Action = "EditAsync",
                ShouldRefresh = true
            };

            return View("_ActionForm", info);
        }

        public async Task<IActionResult> EditAsync(int targetId, string level)
        {
            var server = Manager.GetServers().First();

            return await Task.FromResult(RedirectToAction("ExecuteAsync", "Console", new
            {
                serverId = server.EndPoint,
                command = $"{_appConfig.CommandPrefix}{_setLevelCommandName} @{targetId} {level}"
            }));
        }

        public IActionResult GenerateLoginTokenForm()
        {
            var info = new ActionInfo()
            {
                ActionButtonLabel = "Generate",
                Name = "GenerateLoginToken",
                Action = "GenerateLoginTokenAsync",
                Inputs = new List<InputInfo>()
            };

            return View("_ActionForm", info);
        }

        [Authorize]
        public string GenerateLoginTokenAsync()
        {
            var state = Manager.TokenAuthenticator.GenerateNextToken(Client.NetworkId);
            return string.Format(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_GENERATETOKEN_SUCCESS"], state.Token, $"{state.RemainingTime} {Utilities.CurrentLocalization.LocalizationIndex["GLOBAL_MINUTES"]}", Client.ClientId);
        }

        public IActionResult ChatForm(long id)
        {
            var info = new ActionInfo()
            {
                ActionButtonLabel = Localization["WEBFRONT_ACTION_LABEL_SUBMIT_MESSAGE"],
                Name = "Chat",
                Inputs = new List<InputInfo>
                {
                    new InputInfo()
                    {
                        Name = "message",
                        Type = "text",
                        Label = Localization["WEBFRONT_ACTION_LABEL_MESSAGE"]
                    },
                    new InputInfo()
                    {
                        Name = "id",
                        Value = id.ToString(),
                        Type = "hidden"
                    }
                },
                Action = "ChatAsync"
            };

            return View("_ActionForm", info);
        }

        public async Task<IActionResult> ChatAsync(long id, string message)
        {
            var server = Manager.GetServers().First(_server => _server.EndPoint == id);

            server.ChatHistory.Add(new SharedLibraryCore.Dtos.ChatInfo()
            {
                ClientId = Client.ClientId,
                Message = message,
                Name = Client.Name,
                ServerGame = server.GameName,
                Time = DateTime.Now
            });

            return await Task.FromResult(RedirectToAction("ExecuteAsync", "Console", new
            {
                serverId = server.EndPoint,
                command = $"{_appConfig.CommandPrefix}{_sayCommandName} {message}"
            }));
        }

        public async Task<IActionResult> RecentClientsForm()
        {
            var clients = await Manager.GetClientService().GetRecentClients();
            return View("~/Views/Shared/Components/Client/_RecentClients.cshtml", clients);
        }

        public IActionResult FlagForm()
        {
            var info = new ActionInfo()
            {
                ActionButtonLabel = Localization["WEBFRONT_ACTION_FLAG_NAME"],
                Name = "Flag",
                Inputs = new List<InputInfo>()
                {
                    new InputInfo()
                    {
                      Name = "reason",
                      Label = Localization["WEBFRONT_ACTION_LABEL_REASON"],
                    }
                },
                Action = "FlagAsync",
                ShouldRefresh = true
            };

            return View("_ActionForm", info);
        }

        public async Task<IActionResult> FlagAsync(int targetId, string reason)
        {
            var server = Manager.GetServers().First();

            return await Task.FromResult(RedirectToAction("ExecuteAsync", "Console", new
            {
                serverId = server.EndPoint,
                command = $"{_appConfig.CommandPrefix}{_flagCommandName} @{targetId} {reason}"
            }));
        }

        public IActionResult UnflagForm()
        {
            var info = new ActionInfo()
            {
                ActionButtonLabel = Localization["WEBFRONT_ACTION_UNFLAG_NAME"],
                Name = "Unflag",
                Inputs = new List<InputInfo>()
                {
                    new InputInfo()
                    {
                      Name = "reason",
                      Label = Localization["WEBFRONT_ACTION_LABEL_REASON"],
                    }
                },
                Action = "UnflagAsync",
                ShouldRefresh = true
            };

            return View("_ActionForm", info);
        }

        public async Task<IActionResult> UnflagAsync(int targetId, string reason)
        {
            var server = Manager.GetServers().First();

            return await Task.FromResult(RedirectToAction("ExecuteAsync", "Console", new
            {
                serverId = server.EndPoint,
                command = $"{_appConfig.CommandPrefix}{_unflagCommandName} @{targetId} {reason}"
            }));
        }

        public IActionResult KickForm(int id)
        {
            var info = new ActionInfo()
            {
                ActionButtonLabel = Localization["WEBFRONT_ACTION_KICK_NAME"],
                Name = "Kick",
                Inputs = new List<InputInfo>()
                {
                    new InputInfo()
                    {
                      Name = "reason",
                      Label = Localization["WEBFRONT_ACTION_LABEL_REASON"],
                    },
                    new InputInfo()
                    {
                        Name = "targetId",
                        Type = "hidden",
                        Value = id.ToString()
                    }
                },
                Action = "KickAsync",
                ShouldRefresh = true
            };

            return View("_ActionForm", info);
        }

        public async Task<IActionResult> KickAsync(int targetId, string reason)
        {
            var client = Manager.GetActiveClients().FirstOrDefault(_client => _client.ClientId == targetId);

            if (client == null)
            {
                return BadRequest(Localization["WEBFRONT_ACTION_KICK_DISCONNECT"]);
            }

            return await Task.FromResult(RedirectToAction("ExecuteAsync", "Console", new
            {
                serverId = client.CurrentServer.EndPoint,
                command = $"{_appConfig.CommandPrefix}{_kickCommandName} {client.ClientNumber} {reason}"
            }));
        }
    }
}
