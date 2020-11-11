using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using static SharedLibraryCore.Database.Models.EFClient;

namespace SharedLibraryCore.Commands
{
    /// <summary>
    /// Quits IW4MAdmin
    /// </summary>
    public class QuitCommand : Command
    {
        public QuitCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "quit";
            Description = _translationLookup["COMMANDS_QUIT_DESC"];
            Alias = "q";
            Permission = Permission.Owner;
            RequiresTarget = false;
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            E.Owner.Manager.Stop();
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Restarts IW4MAdmin
    /// </summary>
    public class RestartCommand : Command
    {
        public RestartCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "restart";
            Description = _translationLookup["COMMANDS_RESTART_DESC"];
            Alias = "res";
            Permission = Permission.Owner;
            RequiresTarget = false;
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            E.Owner.Manager.Restart();
            E.Origin.Tell(_translationLookup["COMMANDS_RESTART_SUCCESS"]);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Claims ownership of the server
    /// </summary>
    public class OwnerCommand : Command
    {
        public OwnerCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "owner";
            Description = _translationLookup["COMMANDS_OWNER_DESC"];
            Alias = "iamgod";
            Permission = Permission.User;
            RequiresTarget = false;
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            // they're trying to set the console's permission level... sigh... 
            if (E.Origin.Level == Permission.Console)
            {
                E.Origin.Tell(_translationLookup["COMMANDS_OWNER_IDIOT"]);
                return;
            }

            if (await (E.Owner.Manager.GetClientService() as ClientService).GetOwnerCount() == 0 &&
                !E.Origin.SetLevel(EFClient.Permission.Owner, Utilities.IW4MAdminClient(E.Owner)).Failed)
            {
                E.Origin.Tell(_translationLookup["COMMANDS_OWNER_SUCCESS"]);
            }
            else
            {
                E.Origin.Tell(_translationLookup["COMMANDS_OWNER_FAIL"]);
            }
        }
    }

    /// <summary>
    /// Warns given client for reason
    /// </summary>
    public class WarnCommand : Command
    {
        public WarnCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "warn";
            Description = _translationLookup["COMMANDS_WARN_DESC"];
            Alias = "w";
            Permission = Permission.Trusted;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            };
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            if (E.Target.Warn(E.Data, E.Origin).Failed)
            {
                E.Origin.Tell(_translationLookup["COMMANDS_WARN_FAIL"].FormatExt(E.Target.Name));
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Clears all warnings for given client
    /// </summary>
    public class WarnClearCommand : Command
    {
        public WarnClearCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "warnclear";
            Description = _translationLookup["COMMANDS_WARNCLEAR_DESC"];
            Alias = "wc";
            Permission = Permission.Trusted;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                }
            };
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            if (!E.Target.WarnClear(E.Origin).Failed)
            {
                E.Owner.Broadcast(_translationLookup["COMMANDS_WARNCLEAR_SUCCESS"].FormatExt(E.Target.Name));
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Kicks client for given reason
    /// </summary>
    public class KickCommand : Command
    {
        public KickCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "kick";
            Description = _translationLookup["COMMANDS_KICK_DESC"];
            Alias = "k";
            Permission = Permission.Moderator;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            switch ((await E.Target.Kick(E.Data, E.Origin).WaitAsync(Utilities.DefaultCommandTimeout, E.Owner.Manager.CancellationToken)).FailReason)
            {
                case GameEvent.EventFailReason.None:
                    E.Origin.Tell(_translationLookup["COMMANDS_KICK_SUCCESS"].FormatExt(E.Target.Name));
                    break;
                case GameEvent.EventFailReason.Exception:
                    E.Origin.Tell(_translationLookup["SERVER_ERROR_COMMAND_INGAME"]);
                    break;
                default:
                    E.Origin.Tell(_translationLookup["COMMANDS_KICK_FAIL"].FormatExt(E.Target.Name));
                    break;
            }
        }
    }

    /// <summary>
    /// Prints out a message to all clients on the server
    /// </summary>
    public class SayCommand : Command
    {
        public SayCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "say";
            Description = _translationLookup["COMMANDS_SAY_DESC"];
            Alias = "s";
            Permission = Permission.Moderator;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_MESSAGE"],
                    Required = true
                }
            };
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            E.Owner.Broadcast($"{(E.Owner.GameName == Server.Game.IW4 ? "^:" : "")}{E.Origin.Name} - ^6{E.Data}^7", E.Origin);
            E.Origin.Tell(_translationLookup["COMMANDS_SAY_SUCCESS"]);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Prints out a message to all clients on all servers
    /// </summary>
    public class SayAllCommand : Command
    {
        public SayAllCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "sayall";
            Description = _translationLookup["COMMANDS_SAY_ALL_DESC"];
            Alias = "sa";
            Permission = Permission.Moderator;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_MESSAGE"],
                    Required = true
                }
            };
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            string message = _translationLookup["COMMANDS_SAY_ALL_MESSAGE_FORMAT"].FormatExt(E.Origin.Name, E.Data);

            foreach (var server in E.Owner.Manager.GetServers())
            {
                server.Broadcast(message, E.Origin);
            }

            E.Origin.Tell(_translationLookup["COMMANDS_SAY_SUCCESS"]);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Temporarily bans a client
    /// </summary>
    public class TempBanCommand : Command
    {
        public TempBanCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "tempban";
            Description = _translationLookup["COMMANDS_TEMPBAN_DESC"];
            Alias = "tb";
            Permission = Permission.Administrator;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_DURATION"],
                    Required = true,
                },
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            };
        }

        private static readonly string TempBanRegex = @"([0-9]+\w+)\ (.+)";

        public override async Task ExecuteAsync(GameEvent E)
        {
            var match = Regex.Match(E.Data, TempBanRegex);
            if (match.Success)
            {
                string tempbanReason = match.Groups[2].ToString();
                var length = match.Groups[1].ToString().ParseTimespan();

                if (length > E.Owner.Manager.GetApplicationSettings().Configuration().MaximumTempBanTime)
                {
                    E.Origin.Tell(_translationLookup["COMMANDS_TEMPBAN_FAIL_TOOLONG"]);
                }

                else
                {
                    switch ((await E.Target.TempBan(tempbanReason, length, E.Origin).WaitAsync(Utilities.DefaultCommandTimeout, E.Owner.Manager.CancellationToken)).FailReason)
                    {
                        case GameEvent.EventFailReason.None:
                            E.Origin.Tell(_translationLookup["COMMANDS_TEMPBAN_SUCCESS"].FormatExt(E.Target, length.HumanizeForCurrentCulture()));
                            break;
                        case GameEvent.EventFailReason.Exception:
                            E.Origin.Tell(_translationLookup["SERVER_ERROR_COMMAND_INGAME"]);
                            break;
                        default:
                            E.Origin.Tell(_translationLookup["COMMANDS_TEMPBAN_FAIL"].FormatExt(E.Target.Name));
                            break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Permanently bans a client
    /// </summary>
    public class BanCommand : Command
    {
        public BanCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "ban";
            Description = _translationLookup["COMMANDS_BAN_DESC"];
            Alias = "b";
            Permission = Permission.SeniorAdmin;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            switch ((await E.Target.Ban(E.Data, E.Origin, false).WaitAsync(Utilities.DefaultCommandTimeout, E.Owner.Manager.CancellationToken)).FailReason)
            {
                case GameEvent.EventFailReason.None:
                    E.Origin.Tell(_translationLookup["COMMANDS_BAN_SUCCESS"].FormatExt(E.Target.Name));
                    break;
                case GameEvent.EventFailReason.Exception:
                    E.Origin.Tell(_translationLookup["SERVER_ERROR_COMMAND_INGAME"]);
                    break;
                default:
                    E.Origin.Tell(_translationLookup["COMMANDS_BAN_FAIL"].FormatExt(E.Target.Name));
                    break;
            }
        }
    }

    /// <summary>
    /// Unbans a banned client
    /// </summary>
    public class UnbanCommand : Command
    {
        public UnbanCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "unban";
            Description = _translationLookup["COMMANDS_UNBAN_DESC"];
            Alias = "ub";
            Permission = Permission.SeniorAdmin;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_CLIENTID"],
                    Required = true,
                },
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_REASON"],
                    Required  = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            // todo: don't do the lookup here
            var penalties = await E.Owner.Manager.GetPenaltyService().GetActivePenaltiesAsync(E.Target.AliasLinkId);
            if (penalties.Where(p => p.Type == EFPenalty.PenaltyType.Ban || p.Type == EFPenalty.PenaltyType.TempBan).FirstOrDefault() != null)
            {
                switch ((await E.Target.Unban(E.Data, E.Origin).WaitAsync(Utilities.DefaultCommandTimeout, E.Owner.Manager.CancellationToken)).FailReason)
                {
                    case GameEvent.EventFailReason.None:
                        E.Origin.Tell(_translationLookup["COMMANDS_UNBAN_SUCCESS"].FormatExt(E.Target));
                        break;
                    default:
                        E.Origin.Tell(_translationLookup["SERVER_ERROR_COMMAND_INGAME"]);
                        break;
                }
            }

            else
            {
                E.Origin.Tell(_translationLookup["COMMANDS_UNBAN_FAIL"].FormatExt(E.Target));
            }
        }
    }

    /// <summary>
    /// Prints client information
    /// </summary>
    public class WhoAmICommand : Command
    {
        public WhoAmICommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "whoami";
            Description = _translationLookup["COMMANDS_WHO_DESC"];
            Alias = "who";
            Permission = Permission.User;
            RequiresTarget = false;
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            string you = string.Format("{0} [^3#{1}^7] {2} ^7[^3@{3}^7] ^7[{4}^7] IP: {5}", E.Origin.Name, E.Origin.ClientNumber, E.Origin.NetworkId, E.Origin.ClientId, Utilities.ConvertLevelToColor(E.Origin.Level, E.Origin.ClientPermission.Name), E.Origin.IPAddressString);
            E.Origin.Tell(you);

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// List online clients
    /// </summary>
    public class ListClientsCommand : Command
    {
        public ListClientsCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "list";
            Description = _translationLookup["COMMANDS_LIST_DESC"];
            Alias = "l";
            Permission = Permission.Moderator;
            RequiresTarget = false;
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            StringBuilder playerList = new StringBuilder();
            int count = 0;
            for (int i = 0; i < E.Owner.Clients.Count; i++)
            {
                var P = E.Owner.Clients[i];

                if (P == null)
                {
                    continue;
                }
                // todo: fix spacing
                // todo: make this better :)
                if (P.Masked)
                {
                    playerList.AppendFormat("[^3{0}^7]{3}[^3{1}^7] {2}", Utilities.ConvertLevelToColor(EFClient.Permission.User, P.ClientPermission.Name), P.ClientNumber, P.Name, Utilities.GetSpaces(EFClient.Permission.SeniorAdmin.ToString().Length - EFClient.Permission.User.ToString().Length));
                }
                else
                {
                    playerList.AppendFormat("[^3{0}^7]{3}[^3{1}^7] {2}", Utilities.ConvertLevelToColor(P.Level, P.ClientPermission.Name), P.ClientNumber, P.Name, Utilities.GetSpaces(EFClient.Permission.SeniorAdmin.ToString().Length - P.Level.ToString().Length));
                }

                if (count == 2 || E.Owner.GetClientsAsList().Count == 1)
                {
                    E.Origin.Tell(playerList.ToString());
                    count = 0;
                    playerList = new StringBuilder();
                    continue;
                }

                count++;
            }

            if (playerList.Length > 0)
            {
                E.Origin.Tell(playerList.ToString());
            }

            // todo: make no players response for webfront

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Prints help information
    /// </summary>
    public class HelpCommand : Command
    {
        public HelpCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "help";
            Description = _translationLookup["COMMANDS_HELP_DESC"];
            Alias = "h";
            Permission = Permission.User;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_COMMANDS"],
                    Required = false
                }
            };
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            string cmd = E.Data.Trim();

            if (cmd.Length > 2)
            {
                bool found = false;
                foreach (var command in E.Owner.Manager.GetCommands())
                {
                    if (command.Name == cmd.ToLower() ||
                        command.Alias == cmd.ToLower())
                    {
                        E.Origin.Tell($"[^3{command.Name}^7] {command.Description}");
                        E.Origin.Tell(command.Syntax);
                        found = true;
                    }
                }

                if (!found)
                {
                    E.Origin.Tell(_translationLookup["COMMANDS_HELP_NOTFOUND"]);
                }
            }

            else
            {
                int count = 0;
                StringBuilder helpResponse = new StringBuilder();
                var CommandList = E.Owner.Manager.GetCommands();

                foreach (Command C in CommandList)
                {
                    if (E.Origin.Level >= C.Permission)
                    {
                        helpResponse.Append(" [^3" + C.Name + "^7] ");
                        if (count >= 4)
                        {
                            E.Origin.Tell(helpResponse.ToString());
                            helpResponse = new StringBuilder();
                            count = 0;
                        }
                        count++;
                    }
                }
                E.Origin.Tell(helpResponse.ToString());
                E.Origin.Tell(_translationLookup["COMMANDS_HELP_MOREINFO"]);
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Fast restarts the map
    /// </summary>
    public class FastRestartCommand : Command
    {
        public FastRestartCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "fastrestart";
            Description = _translationLookup["COMMANDS_FASTRESTART_DESC"];
            Alias = "fr";
            Permission = Permission.Moderator;
            RequiresTarget = false;
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Owner.ExecuteCommandAsync("fast_restart");

            var _ = !E.Origin.Masked ?
                  E.Owner.Broadcast($"^5{E.Origin.Name} ^7{_translationLookup["COMMANDS_FASTRESTART_UNMASKED"]}") :
                 E.Owner.Broadcast(_translationLookup["COMMANDS_FASTRESTART_MASKED"]);
        }
    }

    /// <summary>
    /// Cycles to the next map in rotation
    /// </summary>
    public class MapRotateCommand : Command
    {
        public MapRotateCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "maprotate";
            Description = _translationLookup["COMMANDS_MAPROTATE_DESC"];
            Alias = "mr";
            Permission = Permission.Administrator;
            RequiresTarget = false;
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            _ = !E.Origin.Masked ?
                E.Owner.Broadcast($"{_translationLookup["COMMANDS_MAPROTATE"]} [^5{E.Origin.Name}^7]", E.Origin) :
                E.Owner.Broadcast(_translationLookup["COMMANDS_MAPROTATE"], E.Origin);

            await Task.Delay(E.Owner.Manager.GetApplicationSettings().Configuration().MapChangeDelaySeconds * 1000);
            await E.Owner.ExecuteCommandAsync("map_rotate");
        }
    }

    /// <summary>
    /// Sets the level of given client
    /// </summary>
    public class SetLevelCommand : Command
    {
        public SetLevelCommand(CommandConfiguration config, ITranslationLookup translationLookup, ILogger<SetLevelCommand> logger) : base(config, translationLookup)
        {
            Name = "setlevel";
            Description = _translationLookup["COMMANDS_SETLEVEL_DESC"];
            Alias = "sl";
            Permission = Permission.Moderator;
            RequiresTarget = true;
            Arguments = new[]
            {
                 new CommandArgument()
                 {
                     Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                     Required = true
                 },
                 new CommandArgument()
                 {
                     Name = _translationLookup["COMMANDS_ARGS_LEVEL"],
                     Required = true
                 }
            };
            this.logger = logger;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            Permission oldPerm = gameEvent.Target.Level;
            Permission newPerm = Utilities.MatchPermission(gameEvent.Data);
            bool allowMultiOwner = gameEvent.Owner.Manager.GetApplicationSettings().Configuration().EnableMultipleOwners;
            bool steppedPrivileges = gameEvent.Owner.Manager.GetApplicationSettings().Configuration().EnableSteppedHierarchy;
            var targetClient = gameEvent.Target;

            // pre setup logic
            bool canPromoteSteppedPriv = gameEvent.Origin.Level > newPerm || gameEvent.Origin.Level == Permission.Owner;
            bool hasOwner = await gameEvent.Owner.Manager.GetClientService().GetOwnerCount() > 0;

            // trying to set self
            if (gameEvent.Target == gameEvent.Origin)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_SETLEVEL_SELF"]);
                return;
            }

            // origin permission not high enough
            else if (gameEvent.Origin.Level < gameEvent.Target.Level)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_SETLEVEL_PERMISSION"].FormatExt(gameEvent.Target.Name));
                return;
            }

            // trying to set owner without enabling multiple owners
            else if (newPerm == Permission.Owner && !allowMultiOwner && hasOwner)
            {
                // only one owner is allowed
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_SETLEVEL_OWNER"]);
                return;
            }

            // trying to set level when only owner is allowed to
            else if (gameEvent.Origin.Level < Permission.Owner && !steppedPrivileges)
            {
                // only the owner is allowed to set levels
                gameEvent.Origin.Tell($"{_translationLookup["COMMANDS_SETLEVEL_STEPPEDDISABLED"]} ^5{gameEvent.Target.Name}");
                return;
            }

            // stepped privilege is enabled, but the new level is too high
            else if (steppedPrivileges && !canPromoteSteppedPriv)
            {
                // can't promote a client to higher than your current perms
                // or your peer
                gameEvent.Origin.Tell(string.Format(_translationLookup["COMMANDS_SETLEVEL_LEVELTOOHIGH"], gameEvent.Target.Name, (gameEvent.Origin.Level - 1).ToString()));
                return;
            }

            // valid
            else if (newPerm > Permission.Banned)
            {
                targetClient = targetClient.ClientNumber < 0 ?
                    gameEvent.Owner.Manager.GetActiveClients()
                    .FirstOrDefault(c => c.ClientId == targetClient?.ClientId) ?? targetClient : targetClient;

                logger.LogDebug("Beginning set level of client {origin} to {newPermission}", gameEvent.Origin.ToString(), newPerm);

                var result = await targetClient.SetLevel(newPerm, gameEvent.Origin).WaitAsync(Utilities.DefaultCommandTimeout, gameEvent.Owner.Manager.CancellationToken);

                if (result.Failed)
                {
                    using (LogContext.PushProperty("Server", gameEvent.Origin.CurrentServer?.ToString()))
                    {
                        logger.LogWarning("Failed to set level of client {origin}", gameEvent.Origin.ToString());
                    }
                    gameEvent.Origin.Tell(_translationLookup["SERVER_ERROR_COMMAND_INGAME"]);
                    return;
                }

                // inform the client that they were promoted
                // we don't really want to tell them if they're demoted haha
                if (targetClient.IsIngame && newPerm > oldPerm)
                {
                    targetClient.Tell(_translationLookup["COMMANDS_SETLEVEL_SUCCESS_TARGET"].FormatExt(newPerm));
                }

                // inform the origin that the client has been updated
                _ = newPerm < oldPerm ?
                    gameEvent.Origin.Tell(_translationLookup["COMMANDS_SETLEVEL_DEMOTE_SUCCESS"].FormatExt(targetClient.Name)) :
                    gameEvent.Origin.Tell(_translationLookup["COMMANDS_SETLEVEL_SUCCESS"].FormatExt(targetClient.Name));
            }

            // all other tests failed so it's invalid group
            else
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_SETLEVEL_FAIL"]);
            }
        }
    }

    /// <summary>
    /// Prints the amount of memory IW4MAdmin is using
    /// </summary>
    public class MemoryUsageCommand : Command
    {
        public MemoryUsageCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "usage";
            Description = _translationLookup["COMMANDS_USAGE_DESC"];
            Alias = "us";
            Permission = Permission.Moderator;
            RequiresTarget = false;
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            E.Origin.Tell(_translationLookup["COMMANDS_USAGE_TEXT"].FormatExt(Math.Round(((System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 2048f) / 1200f), 1)));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Prints out how long IW4MAdmin has been running
    /// </summary>
    public class UptimeCommand : Command
    {
        public UptimeCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "uptime";
            Description = _translationLookup["COMMANDS_UPTIME_DESC"];
            Alias = "up";
            Permission = Permission.Moderator;
            RequiresTarget = false;
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            TimeSpan uptime = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
            var loc = _translationLookup;
            E.Origin.Tell(loc["COMMANDS_UPTIME_TEXT"].FormatExt(uptime.Days, uptime.Hours, uptime.Minutes));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Lists all unmasked admins
    /// </summary>
    public class ListAdminsCommand : Command
    {
        public ListAdminsCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "admins";
            Description = _translationLookup["COMMANDS_ADMINS_DESC"];
            Alias = "a";
            Permission = Permission.User;
            RequiresTarget = false;
        }

        public static string OnlineAdmins(Server S, ITranslationLookup lookup)
        {
            var onlineAdmins = S.GetClientsAsList()
                .Where(p => p.Level > Permission.Flagged)
                .Where(p => !p.Masked)
                .Select(p => $"[^3{Utilities.ConvertLevelToColor(p.Level, p.ClientPermission.Name)}^7] {p.Name}");

            return onlineAdmins.Count() > 0 ?
                string.Join(Environment.NewLine, onlineAdmins) :
                lookup["COMMANDS_ADMINS_NONE"];
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            foreach (string line in OnlineAdmins(E.Owner, _translationLookup).Split(Environment.NewLine))
            {
                var _ = E.Message.IsBroadcastCommand(_config.BroadcastCommandPrefix) ? E.Owner.Broadcast(line) : E.Origin.Tell(line);
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Attempts to load the specified map
    /// </summary>
    public class LoadMapCommand : Command
    {
        public LoadMapCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "map";
            Description = _translationLookup["COMMANDS_MAP_DESC"];
            Alias = "m";
            Permission = Permission.Administrator;
            RequiresTarget = false;
            Arguments = new[]
            {
                 new CommandArgument()
                 {
                     Name = _translationLookup["COMMANDS_ARGS_MAP"],
                     Required = true
                 }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            string newMap = E.Data.Trim();
            int delay = E.Owner.Manager.GetApplicationSettings().Configuration().MapChangeDelaySeconds * 1000;

            var foundMap = E.Owner.Maps.FirstOrDefault(_map => _map.Name.Equals(newMap, StringComparison.InvariantCultureIgnoreCase) ||
            _map.Alias.Equals(newMap, StringComparison.InvariantCultureIgnoreCase));

            _ = foundMap == null ?
                E.Owner.Broadcast(_translationLookup["COMMANDS_MAP_UKN"].FormatExt(newMap)) :
                 E.Owner.Broadcast(_translationLookup["COMMANDS_MAP_SUCCESS"].FormatExt(foundMap.Alias));

            await Task.Delay(delay);
            await E.Owner.LoadMap(foundMap?.Name ?? newMap);
        }
    }

    /// <summary>
    /// Finds player by name
    /// </summary>
    public class FindPlayerCommand : Command
    {
        public FindPlayerCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "find";
            Description = _translationLookup["COMMANDS_FIND_DESC"];
            Alias = "f";
            Permission = Permission.Administrator;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Data.Length < 3)
            {
                E.Origin.Tell(_translationLookup["COMMANDS_FIND_MIN"]);
                return;
            }

            var db_players = (await (E.Owner.Manager.GetClientService() as ClientService).FindClientsByIdentifier(E.Data));

            if (db_players.Count == 0)
            {
                E.Origin.Tell(_translationLookup["COMMANDS_FIND_EMPTY"]);
                return;
            }

            foreach (var client in db_players)
            {
                E.Origin.Tell(_translationLookup["COMMANDS_FIND_FORMAT"].FormatExt(client.Name, client.ClientId, Utilities.ConvertLevelToColor((Permission)client.LevelInt, client.Level), client.IPAddress, client.LastConnectionText));
            }
        }
    }

    /// <summary>
    /// Lists server and global rules
    /// </summary>
    public class ListRulesCommands : Command
    {
        public ListRulesCommands(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "rules";
            Description = _translationLookup["COMMANDS_RULES_DESC"];
            Alias = "r";
            Permission = Permission.User;
            RequiresTarget = false;
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            if (E.Owner.Manager.GetApplicationSettings().Configuration().GlobalRules?.Length < 1 &&
                E.Owner.ServerConfig.Rules?.Length < 1)
            {
                var _ = E.Message.IsBroadcastCommand(_config.BroadcastCommandPrefix) ?
                      E.Owner.Broadcast(_translationLookup["COMMANDS_RULES_NONE"]) :
                      E.Origin.Tell(_translationLookup["COMMANDS_RULES_NONE"]);
            }

            else
            {
                var rules = new List<string>();
                rules.AddRange(E.Owner.Manager.GetApplicationSettings().Configuration().GlobalRules);
                if (E.Owner.ServerConfig.Rules != null)
                {
                    rules.AddRange(E.Owner.ServerConfig.Rules);
                }

                foreach (string r in rules)
                {
                    var _ = E.Message.IsBroadcastCommand(_config.BroadcastCommandPrefix) ? E.Owner.Broadcast($"- {r}") : E.Origin.Tell($"- {r}");
                }
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Sends a private message to another player
    /// </summary>
    public class PrivateMessageCommand : Command
    {
        public PrivateMessageCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "privatemessage";
            Description = _translationLookup["COMMANDS_PM_DESC"];
            Alias = "pm";
            Permission = Permission.User;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_MESSAGE"],
                    Required = true
                }
            };
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            E.Target.Tell($"^1{E.Origin.Name} ^3[PM]^7 - {E.Data}");
            E.Origin.Tell($"To ^3{E.Target.Name} ^7-> {E.Data}");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Flag given client for specified reason
    /// </summary>
    public class FlagClientCommand : Command
    {
        public FlagClientCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "flag";
            Description = _translationLookup["COMMANDS_FLAG_DESC"];
            Alias = "fp";
            Permission = Permission.Moderator;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            switch ((await E.Target.Flag(E.Data, E.Origin).WaitAsync(Utilities.DefaultCommandTimeout, E.Owner.Manager.CancellationToken)).FailReason)
            {
                case GameEvent.EventFailReason.Permission:
                    E.Origin.Tell(_translationLookup["COMMANDS_FLAG_FAIL"].FormatExt(E.Target.Name));
                    break;
                case GameEvent.EventFailReason.Invalid:
                    E.Origin.Tell($"{_translationLookup["COMMANDS_FLAG_ALREADYFLAGGED"]}");
                    break;
                case GameEvent.EventFailReason.None:
                    E.Origin.Tell(_translationLookup["COMMANDS_FLAG_SUCCESS"].FormatExt(E.Target.Name));
                    break;
                default:
                    E.Origin.Tell(_translationLookup["SERVER_ERROR_COMMAND_INGAME"]);
                    break;
            }
        }
    }

    /// <summary>
    /// Unflag given client for specified reason
    /// </summary>
    public class UnflagClientCommand : Command
    {
        public UnflagClientCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "unflag";
            Description = _translationLookup["COMMANDS_UNFLAG_DESC"];
            Alias = "uf";
            Permission = Permission.Moderator;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            switch ((await E.Target.Unflag(E.Data, E.Origin).WaitAsync(Utilities.DefaultCommandTimeout, E.Owner.Manager.CancellationToken)).FailReason)
            {
                case GameEvent.EventFailReason.None:
                    E.Origin.Tell(_translationLookup["COMMANDS_FLAG_UNFLAG"].FormatExt(E.Target.Name));
                    break;
                case GameEvent.EventFailReason.Permission:
                    E.Origin.Tell(_translationLookup["COMMANDS_UNFLAG_FAIL"].FormatExt(E.Target.Name));
                    break;
                case GameEvent.EventFailReason.Invalid:
                    E.Origin.Tell(_translationLookup["COMMANDS_UNFLAG_NOTFLAGGED"]);
                    break;
                default:
                    E.Origin.Tell(_translationLookup["SERVER_ERROR_COMMAND_INGAME"]);
                    break;
            }
        }
    }

    /// <summary>
    /// Report client for given reason
    /// </summary>
    public class ReportClientCommand : Command
    {
        public ReportClientCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "report";
            Description = _translationLookup["COMMANDS_REPORT_DESC"];
            Alias = "rep";
            Permission = Permission.User;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent commandEvent)
        {
            if (commandEvent.Data.ToLower().Contains("camp"))
            {
                commandEvent.Origin.Tell(_translationLookup["COMMANDS_REPORT_FAIL_CAMP"]);
                return;
            }

            bool success = false;

            switch ((await commandEvent.Target.Report(commandEvent.Data, commandEvent.Origin).WaitAsync(Utilities.DefaultCommandTimeout, commandEvent.Owner.Manager.CancellationToken)).FailReason)
            {
                case GameEvent.EventFailReason.None:
                    commandEvent.Origin.Tell(_translationLookup["COMMANDS_REPORT_SUCCESS"]);
                    success = true;
                    break;
                case GameEvent.EventFailReason.Exception:
                    commandEvent.Origin.Tell(_translationLookup["COMMANDS_REPORT_FAIL_DUPLICATE"]);
                    break;
                case GameEvent.EventFailReason.Permission:
                    commandEvent.Origin.Tell(_translationLookup["COMMANDS_REPORT_FAIL"].FormatExt(commandEvent.Target.Name));
                    break;
                case GameEvent.EventFailReason.Invalid:
                    commandEvent.Origin.Tell(_translationLookup["COMMANDS_REPORT_FAIL_SELF"]);
                    break;
                case GameEvent.EventFailReason.Throttle:
                    commandEvent.Origin.Tell(_translationLookup["COMMANDS_REPORT_FAIL_TOOMANY"]);
                    break;
            }

            if (success)
            {
                commandEvent.Owner.ToAdmins(String.Format("^5{0}^7->^1{1}^7: {2}", commandEvent.Origin.Name, commandEvent.Target.Name, commandEvent.Data));
            }
        }
    }

    /// <summary>
    /// List all reports on the server
    /// </summary>
    public class ListReportsCommand : Command
    {
        public ListReportsCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "reports";
            Description = _translationLookup["COMMANDS_REPORTS_DESC"];
            Alias = "reps";
            Permission = Permission.Moderator;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_CLEAR"],
                    Required = false
                }
            };
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            if (E.Data != null && E.Data.ToLower().Contains(_translationLookup["COMMANDS_ARGS_CLEAR"]))
            {
                E.Owner.Reports = new List<Report>();
                E.Origin.Tell(_translationLookup["COMMANDS_REPORTS_CLEAR_SUCCESS"]);
                return Task.CompletedTask;
            }

            if (E.Owner.Reports.Count < 1)
            {
                E.Origin.Tell(_translationLookup["COMMANDS_REPORTS_NONE"]);
                return Task.CompletedTask;
            }

            foreach (Report R in E.Owner.Reports)
            {
                E.Origin.Tell(String.Format("^5{0}^7->^1{1}^7: {2}", R.Origin.Name, R.Target.Name, R.Reason));
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Masks client from announcements and online admin list
    /// </summary>
    public class MaskCommand : Command
    {
        public MaskCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "mask";
            Description = _translationLookup["COMMANDS_MASK_DESC"];
            Alias = "hide";
            Permission = EFClient.Permission.Moderator;
            RequiresTarget = false;
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Origin.Masked)
            {
                E.Origin.Masked = false;
                E.Origin.Tell(_translationLookup["COMMANDS_MASK_OFF"]);
            }
            else
            {
                E.Origin.Masked = true;
                E.Origin.Tell(_translationLookup["COMMANDS_MASK_ON"]);
            }

            await E.Owner.Manager.GetClientService().Update(E.Origin);
        }
    }

    /// <summary>
    /// Lists ban information for given client
    /// </summary>
    public class ListBanInfoCommand : Command
    {
        public ListBanInfoCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "baninfo";
            Description = _translationLookup["COMMANDS_BANINFO_DESC"];
            Alias = "bi";
            Permission = Permission.Moderator;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var existingPenalties = await E.Owner.Manager.GetPenaltyService().GetActivePenaltiesAsync(E.Target.AliasLinkId, E.Target.IPAddress);
            var penalty = existingPenalties.FirstOrDefault(b => b.Type > EFPenalty.PenaltyType.Kick);

            if (penalty == null)
            {
                E.Origin.Tell(_translationLookup["COMMANDS_BANINFO_NONE"]);
                return;
            }

            if (penalty.Type == EFPenalty.PenaltyType.Ban)
            {
                E.Origin.Tell(_translationLookup["COMMANDS_BANINFO_SUCCESS"].FormatExt(E.Target.Name, penalty.Offense));
            }

            else
            {
                string remainingTime = (penalty.Expires.Value - DateTime.UtcNow).HumanizeForCurrentCulture();
                E.Origin.Tell(_translationLookup["COMMANDS_BANINFO_TB_SUCCESS"].FormatExt(E.Target.Name, penalty.Offense, remainingTime));
            }
        }
    }

    /// <summary>
    /// Lists alises of specified client
    /// </summary>
    public class ListAliasesCommand : Command
    {
        public ListAliasesCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "alias";
            Description = _translationLookup["COMMANDS_ALIAS_DESC"];
            Alias = "known";
            Permission = EFClient.Permission.Moderator;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true,
                }
            };
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            StringBuilder message = new StringBuilder();
            var names = new List<string>(E.Target.AliasLink.Children.Select(a => a.Name));
            var IPs = new List<string>(E.Target.AliasLink.Children.Select(a => a.IPAddress.ConvertIPtoString()).Distinct());

            E.Origin.Tell($"[^3{E.Target}^7]");

            message.Append($"{_translationLookup["COMMANDS_ALIAS_ALIASES"]}: ");
            message.Append(String.Join(" | ", names));
            E.Origin.Tell(message.ToString());

            message.Clear();
            message.Append($"{_translationLookup["COMMANDS_ALIAS_IPS"]}: ");
            message.Append(String.Join(" | ", IPs));
            E.Origin.Tell(message.ToString());

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Executes RCon command
    /// </summary>
    public class ExecuteRConCommand : Command
    {
        public ExecuteRConCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "rcon";
            Description = _translationLookup["COMMANDS_RCON_DESC"];
            Alias = "rcon";
            Permission = Permission.Owner;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_COMMANDS"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var Response = await E.Owner.ExecuteCommandAsync(E.Data.Trim());
            foreach (string S in Response)
            {
                E.Origin.Tell(S);
            }

            if (Response.Length == 0)
            {
                E.Origin.Tell(_translationLookup["COMMANDS_RCON_SUCCESS"]);
            }
        }
    }

    /// <summary>
    /// Lists the loaded plugins
    /// </summary>
    /*public class ListPluginsCommand : Command
    {
        private readonly IPluginImporter _pluginImporter;
        public ListPluginsCommand(CommandConfiguration config, ITranslationLookup translationLookup, IPluginImporter pluginImporter) : base(config, translationLookup)
        {
            Name = "plugins";
            Description = _translationLookup["COMMANDS_PLUGINS_DESC"];
            Alias = "p";
            Permission = Permission.Administrator;
            RequiresTarget = false;
            _pluginImporter = pluginImporter;
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            E.Origin.Tell(_translationLookup["COMMANDS_PLUGINS_LOADED"]);
            foreach (var P in _pluginImporter.ActivePlugins)
            {
                E.Origin.Tell(string.Format("^3{0} ^7[v^3{1}^7] by ^5{2}^7", P.Name, P.Version, P.Author));
            }
            return Task.CompletedTask;
        }
    }*/

    /// <summary>
    /// Lists external IP
    /// </summary>
    public class ListExternalIPCommand : Command
    {
        public ListExternalIPCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "getexternalip";
            Description = _translationLookup["COMMANDS_IP_DESC"];
            Alias = "ip";
            Permission = Permission.User;
            RequiresTarget = false;
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            E.Origin.Tell(_translationLookup["COMMANDS_IP_SUCCESS"].FormatExt(E.Origin.IPAddressString));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Prunes inactive privileged clients
    /// </summary>
    public class PruneAdminsCommand : Command
    {
        public PruneAdminsCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "prune";
            Description = _translationLookup["COMMANDS_PRUNE_DESC"];
            Alias = "pa";
            Permission = Permission.Owner;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_INACTIVE"],
                    Required = false
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            int inactiveDays = 30;

            try
            {
                if (E.Data.Length > 0)
                {
                    inactiveDays = int.Parse(E.Data);
                    if (inactiveDays < 1)
                    {
                        throw new FormatException();
                    }
                }
            }

            catch (FormatException)
            {
                E.Origin.Tell(_translationLookup["COMMANDS_PRUNE_FAIL"]);
                return;
            }

            List<EFClient> inactiveUsers = null;
            // todo: make an event for this
            // update user roles
            using (var context = new DatabaseContext())
            {
                var lastActive = DateTime.UtcNow.AddDays(-inactiveDays);
                inactiveUsers = await context.Clients
                    .Where(c => c.Level > Permission.Flagged && c.Level <= Permission.Moderator)
                    .Where(c => c.LastConnection < lastActive)
                    .ToListAsync();
                inactiveUsers.ForEach(c => c.SetLevel(Permission.User, E.Origin));
                await context.SaveChangesAsync();
            }
            E.Origin.Tell(_translationLookup["COMMANDS_PRUNE_SUCCESS"].FormatExt(inactiveUsers.Count));
        }
    }


    /// <summary>
    /// Sets login password
    /// </summary>
    public class SetPasswordCommand : Command
    {
        public SetPasswordCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "setpassword";
            Description = _translationLookup["COMMANDS_SETPASSWORD_DESC"];
            Alias = "sp";
            Permission = Permission.Moderator;
            RequiresTarget = false;
            AllowImpersonation = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_PASSWORD"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Data.Length < 5)
            {
                E.Origin.Tell(_translationLookup["COMMANDS_PASSWORD_FAIL"]);
                return;
            }

            string[] hashedPassword = Helpers.Hashing.Hash(E.Data);

            E.Origin.Password = hashedPassword[0];
            E.Origin.PasswordSalt = hashedPassword[1];

            await E.Owner.Manager.GetClientService().Update(E.Origin);
            E.Origin.Tell(_translationLookup["COMMANDS_PASSWORD_SUCCESS"]);
        }
    }

    /// <summary>
    /// Gets the ping of a client
    /// </summary>
    public class GetClientPingCommand : Command
    {
        public GetClientPingCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "ping";
            Description = _translationLookup["COMMANDS_PING_DESC"];
            Alias = "pi";
            Permission = Permission.User;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = false
                }
            };
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            if (E.Target == null)
            {
                E.Target = E.Owner.GetClientByName(E.Data).FirstOrDefault();
            }

            if (E.Target == null)
            {
                E.Origin.Tell(_translationLookup["COMMANDS_PING_SELF"].FormatExt(E.Origin.Ping));
            }
            else
            {
                E.Origin.Tell(_translationLookup["COMMANDS_PING_TARGET"].FormatExt(E.Target.Name, E.Target.Ping));
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Sets the email for gravatar in webfront
    /// </summary>
    public class SetGravatarCommand : Command
    {
        private readonly IMetaService _metaService;

        public SetGravatarCommand(CommandConfiguration config, ITranslationLookup translationLookup, IMetaService metaService) : base(config, translationLookup)
        {
            Name = "setgravatar";
            Description = _translationLookup["COMMANDS_GRAVATAR_DESC"];
            Alias = "sg";
            Permission = Permission.User;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_GRAVATAR"],
                    Required = true
                }
            };

            _metaService = metaService;
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            using (var md5 = MD5.Create())
            {
                string gravatarEmail = string.Concat(md5.ComputeHash(E.Data.ToLower().Select(d => Convert.ToByte(d)).ToArray())
                                .Select(h => h.ToString("x2")));
                await _metaService.AddPersistentMeta("GravatarEmail", gravatarEmail, E.Origin);
            }

            E.Origin.Tell(_translationLookup["COMMANDS_GRAVATAR_SUCCESS_NEW"]);
        }
    }

    /// <summary>
    /// Retrieves the next map in rotation
    /// </summary>
    public class NextMapCommand : Command
    {
        public NextMapCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "nextmap";
            Description = _translationLookup["COMMANDS_NEXTMAP_DESC"];
            Alias = "nm";
            Permission = EFClient.Permission.User;
            RequiresTarget = false;
        }

        public static async Task<string> GetNextMap(Server s, ITranslationLookup lookup)
        {
            string mapRotation = (await s.GetDvarAsync<string>("sv_mapRotation")).Value?.ToLower() ?? "";
            var regexMatches = Regex.Matches(mapRotation, @"((?:gametype|exec) +(?:([a-z]{1,4})(?:.cfg)?))? *map ([a-z|_|\d]+)", RegexOptions.IgnoreCase).ToList();

            // find the current map in the rotation
            var currentMap = regexMatches.Where(m => m.Groups[3].ToString() == s.CurrentMap.Name);
            var lastMap = regexMatches.LastOrDefault();
            Map nextMap = null;

            // no maprotation at all
            if (regexMatches.Count() == 0)
            {
                return lookup["COMMANDS_NEXTMAP_SUCCESS"].FormatExt(s.CurrentMap.Alias, Utilities.GetLocalizedGametype(s.Gametype));
            }

            // the current map is not in rotation
            if (currentMap.Count() == 0)
            {
                return lookup["COMMANDS_NEXTMAP_NOT_IN_ROTATION"];
            }

            // there's duplicate maps in rotation
            else if (currentMap.Count() > 1)
            {
                // gametype has been manually specified
                var duplicateMaps = currentMap.Where(m => !string.IsNullOrEmpty(m.Groups[1].ToString()));

                // more than one instance of map in rotation
                if (duplicateMaps.Count() > 0)
                {
                    currentMap = duplicateMaps.Where(m => m.Groups[2].ToString() == s.Gametype);
                }

                // else we just have to assume it's the first one
            }

            // if the current map is the last map, the next map is the first map
            var nextMapMatch = currentMap.First().Index != lastMap.Index ?
               regexMatches[regexMatches.IndexOf(currentMap.First()) + 1] :
               regexMatches.First();

            string nextMapName = nextMapMatch.Groups[3].ToString();

            nextMap = s.Maps.FirstOrDefault(m => m.Name == nextMapMatch.Groups[3].ToString()) ?? new Map() { Alias = nextMapName, Name = nextMapName };
            string nextGametype = nextMapMatch.Groups[2].ToString().Length == 0 ?
                Utilities.GetLocalizedGametype(s.Gametype) :
                Utilities.GetLocalizedGametype(nextMapMatch.Groups[2].ToString());

            return lookup["COMMANDS_NEXTMAP_SUCCESS"].FormatExt(nextMap.Alias, nextGametype);
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            E.Origin.Tell(await GetNextMap(E.Owner, _translationLookup));
        }
    }
}