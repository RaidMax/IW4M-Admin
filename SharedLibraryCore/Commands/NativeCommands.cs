using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using static Data.Models.Client.EFClient;

namespace SharedLibraryCore.Commands
{
    /// <summary>
    ///     Quits IW4MAdmin
    /// </summary>
    public class QuitCommand : Command
    {
        public QuitCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "quit";
            Description = _translationLookup["COMMANDS_QUIT_DESC"];
            Alias = "q";
            Permission = Permission.Owner;
            RequiresTarget = false;
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Owner.Manager.Stop();
        }
    }

    /// <summary>
    ///     Restarts IW4MAdmin
    /// </summary>
    public class RestartCommand : Command
    {
        public RestartCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
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
    ///     Claims ownership of the server
    /// </summary>
    public class OwnerCommand : Command
    {
        public OwnerCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
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

            if (await E.Owner.Manager.GetClientService().GetOwnerCount() == 0 &&
                !E.Origin.SetLevel(Permission.Owner, Utilities.IW4MAdminClient(E.Owner)).Failed)
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
    ///     Warns given client for reason
    /// </summary>
    public class WarnCommand : Command
    {
        private readonly ApplicationConfiguration _appConfig;

        public WarnCommand(ApplicationConfiguration appConfig, CommandConfiguration config,
            ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "warn";
            Description = _translationLookup["COMMANDS_WARN_DESC"];
            Alias = "w";
            Permission = Permission.Trusted;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            };
            _appConfig = appConfig;
        }

        public override Task ExecuteAsync(GameEvent gameEvent)
        {
            var reason = gameEvent.Data.FindRuleForReason(_appConfig, gameEvent.Owner);
            if (gameEvent.Target.Warn(reason, gameEvent.Origin).Failed)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_WARN_FAIL"].FormatExt(gameEvent.Target.Name));
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    ///     Clears all warnings for given client
    /// </summary>
    public class WarnClearCommand : Command
    {
        public WarnClearCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "warnclear";
            Description = _translationLookup["COMMANDS_WARNCLEAR_DESC"];
            Alias = "wc";
            Permission = Permission.Trusted;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument
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
    ///     Kicks client for given reason
    /// </summary>
    public class KickCommand : Command
    {
        private readonly ApplicationConfiguration _appConfig;

        public KickCommand(ApplicationConfiguration appConfig, CommandConfiguration config,
            ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "kick";
            Description = _translationLookup["COMMANDS_KICK_DESC"];
            Alias = "k";
            Permission = Permission.Moderator;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            };
            _appConfig = appConfig;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            var reason = gameEvent.Data.FindRuleForReason(_appConfig, gameEvent.Owner);
            switch ((await gameEvent.Target.Kick(reason, gameEvent.Origin).WaitAsync(Utilities.DefaultCommandTimeout,
                        gameEvent.Owner.Manager.CancellationToken)).FailReason)
            {
                case GameEvent.EventFailReason.None:
                    gameEvent.Origin.Tell(_translationLookup["COMMANDS_KICK_SUCCESS"].FormatExt(gameEvent.Target.Name));
                    break;
                case GameEvent.EventFailReason.Exception:
                    gameEvent.Origin.Tell(_translationLookup["SERVER_ERROR_COMMAND_INGAME"]);
                    break;
                default:
                    gameEvent.Origin.Tell(_translationLookup["COMMANDS_KICK_FAIL"].FormatExt(gameEvent.Target.Name));
                    break;
            }
        }
    }

    /// <summary>
    ///     Temporarily bans a client
    /// </summary>
    public class TempBanCommand : Command
    {
        private static readonly string TempBanRegex = @"([0-9]+\w+)\ (.+)";
        private readonly ApplicationConfiguration _appConfig;

        public TempBanCommand(ApplicationConfiguration appConfig, CommandConfiguration config,
            ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "tempban";
            Description = _translationLookup["COMMANDS_TEMPBAN_DESC"];
            Alias = "tb";
            Permission = Permission.Administrator;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_DURATION"],
                    Required = true
                },
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            };
            _appConfig = appConfig;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            var match = Regex.Match(gameEvent.Data, TempBanRegex);
            if (match.Success)
            {
                var tempbanReason = match.Groups[2].ToString().FindRuleForReason(_appConfig, gameEvent.Owner);
                var length = match.Groups[1].ToString().ParseTimespan();

                if (length > gameEvent.Owner.Manager.GetApplicationSettings().Configuration().MaximumTempBanTime)
                {
                    gameEvent.Origin.Tell(_translationLookup["COMMANDS_TEMPBAN_FAIL_TOOLONG"]);
                }

                else
                {
                    switch ((await gameEvent.Target.TempBan(tempbanReason, length, gameEvent.Origin)
                                .WaitAsync(Utilities.DefaultCommandTimeout, gameEvent.Owner.Manager.CancellationToken))
                            .FailReason)
                    {
                        case GameEvent.EventFailReason.None:
                            gameEvent.Origin.Tell(_translationLookup["COMMANDS_TEMPBAN_SUCCESS"]
                                .FormatExt(gameEvent.Target, length.HumanizeForCurrentCulture()));
                            break;
                        case GameEvent.EventFailReason.Exception:
                            gameEvent.Origin.Tell(_translationLookup["SERVER_ERROR_COMMAND_INGAME"]);
                            break;
                        default:
                            gameEvent.Origin.Tell(_translationLookup["COMMANDS_TEMPBAN_FAIL"]
                                .FormatExt(gameEvent.Target.Name));
                            break;
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Permanently bans a client
    /// </summary>
    public class BanCommand : Command
    {
        private readonly ApplicationConfiguration _appConfig;

        public BanCommand(ApplicationConfiguration appConfig, CommandConfiguration config,
            ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "ban";
            Description = _translationLookup["COMMANDS_BAN_DESC"];
            Alias = "b";
            Permission = Permission.SeniorAdmin;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            };
            _appConfig = appConfig;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            var reason = gameEvent.Data.FindRuleForReason(_appConfig, gameEvent.Owner);
            switch ((await gameEvent.Target.Ban(reason, gameEvent.Origin, false)
                        .WaitAsync(Utilities.DefaultCommandTimeout, gameEvent.Owner.Manager.CancellationToken))
                    .FailReason)
            {
                case GameEvent.EventFailReason.None:
                    gameEvent.Origin.Tell(_translationLookup["COMMANDS_BAN_SUCCESS"].FormatExt(gameEvent.Target.Name));
                    break;
                case GameEvent.EventFailReason.Exception:
                    gameEvent.Origin.Tell(_translationLookup["SERVER_ERROR_COMMAND_INGAME"]);
                    break;
                default:
                    gameEvent.Origin.Tell(_translationLookup["COMMANDS_BAN_FAIL"].FormatExt(gameEvent.Target.Name));
                    break;
            }
        }
    }

    /// <summary>
    ///     Unbans a banned client
    /// </summary>
    public class UnbanCommand : Command
    {
        public UnbanCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "unban";
            Description = _translationLookup["COMMANDS_UNBAN_DESC"];
            Alias = "ub";
            Permission = Permission.SeniorAdmin;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_CLIENTID"],
                    Required = true
                },
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            // todo: don't do the lookup here
            var penalties = await gameEvent.Owner.Manager.GetPenaltyService().GetActivePenaltiesAsync(gameEvent.Target.AliasLinkId,
                gameEvent.Target.CurrentAliasId, gameEvent.Target.NetworkId, gameEvent.Target.GameName, gameEvent.Target.CurrentAlias.IPAddress);
            
            if (penalties
                    .FirstOrDefault(p =>
                        p.Type == EFPenalty.PenaltyType.Ban || p.Type == EFPenalty.PenaltyType.TempBan) != null)
            {
                switch ((await gameEvent.Target.Unban(gameEvent.Data, gameEvent.Origin)
                            .WaitAsync(Utilities.DefaultCommandTimeout, gameEvent.Owner.Manager.CancellationToken)).FailReason)
                {
                    case GameEvent.EventFailReason.None:
                        gameEvent.Origin.Tell(_translationLookup["COMMANDS_UNBAN_SUCCESS"].FormatExt(gameEvent.Target));
                        break;
                    default:
                        gameEvent.Origin.Tell(_translationLookup["SERVER_ERROR_COMMAND_INGAME"]);
                        break;
                }
            }

            else
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_UNBAN_FAIL"].FormatExt(gameEvent.Target));
            }
        }
    }

    /// <summary>
    ///     Fast restarts the map
    /// </summary>
    public class FastRestartCommand : Command
    {
        public FastRestartCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
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

            var _ = !E.Origin.Masked
                ? E.Owner.Broadcast(
                    $"(Color::Accent){E.Origin.Name} (Color::White){_translationLookup["COMMANDS_FASTRESTART_UNMASKED"]}")
                : E.Owner.Broadcast(_translationLookup["COMMANDS_FASTRESTART_MASKED"]);
        }
    }

    /// <summary>
    ///     Cycles to the next map in rotation
    /// </summary>
    public class MapRotateCommand : Command
    {
        public MapRotateCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "maprotate";
            Description = _translationLookup["COMMANDS_MAPROTATE_DESC"];
            Alias = "mr";
            Permission = Permission.Administrator;
            RequiresTarget = false;
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            _ = !E.Origin.Masked
                ? E.Owner.Broadcast(
                    $"{_translationLookup["COMMANDS_MAPROTATE"]} [(Color::Accent){E.Origin.Name}(Color::White)]",
                    E.Origin)
                : E.Owner.Broadcast(_translationLookup["COMMANDS_MAPROTATE"], E.Origin);

            await Task.Delay(E.Owner.Manager.GetApplicationSettings().Configuration().MapChangeDelaySeconds * 1000);
            await E.Owner.ExecuteCommandAsync("map_rotate");
        }
    }

    /// <summary>
    ///     Sets the level of given client
    /// </summary>
    public class SetLevelCommand : Command
    {
        public SetLevelCommand(CommandConfiguration config, ITranslationLookup translationLookup,
            ILogger<SetLevelCommand> logger) : base(config, translationLookup)
        {
            Name = "setlevel";
            Description = _translationLookup["COMMANDS_SETLEVEL_DESC"];
            Alias = "sl";
            Permission = Permission.Moderator;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_LEVEL"],
                    Required = true
                }
            };
            this.logger = logger;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            var oldPerm = gameEvent.Target.Level;
            var newPerm = Utilities.MatchPermission(gameEvent.Data);
            var allowMultiOwner = gameEvent.Owner.Manager.GetApplicationSettings().Configuration().EnableMultipleOwners;
            var steppedPrivileges =
                gameEvent.Owner.Manager.GetApplicationSettings().Configuration().EnableSteppedHierarchy;
            var targetClient = gameEvent.Target;

            // pre setup logic
            var canPromoteSteppedPriv = gameEvent.Origin.Level > newPerm || gameEvent.Origin.Level == Permission.Owner;
            var hasOwner = await gameEvent.Owner.Manager.GetClientService().GetOwnerCount() > 0;

            // trying to set self
            if (gameEvent.Target == gameEvent.Origin)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_SETLEVEL_SELF"]);
            }

            // origin permission not high enough
            else if (gameEvent.Origin.Level < gameEvent.Target.Level)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_SETLEVEL_PERMISSION"]
                    .FormatExt(gameEvent.Target.Name));
            }

            // trying to set owner without enabling multiple owners
            else if (newPerm == Permission.Owner && !allowMultiOwner && hasOwner)
            {
                // only one owner is allowed
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_SETLEVEL_OWNER"]);
            }

            // trying to set level when only owner is allowed to
            else if (gameEvent.Origin.Level < Permission.Owner && !steppedPrivileges)
            {
                // only the owner is allowed to set levels
                gameEvent.Origin.Tell(
                    $"{_translationLookup["COMMANDS_SETLEVEL_STEPPEDDISABLED"]} (Color::White){gameEvent.Target.Name}");
            }

            else if (gameEvent.Target.Level == Permission.Flagged)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_SETLEVEL_FLAGGED"]
                    .FormatExt(gameEvent.Target.Name + "(Color::White)"));
            }

            // stepped privilege is enabled, but the new level is too high
            else if (steppedPrivileges && !canPromoteSteppedPriv)
            {
                // can't promote a client to higher than your current perms
                // or your peer
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_SETLEVEL_LEVELTOOHIGH_V2"]
                    .FormatExt(gameEvent.Target.Name, (gameEvent.Origin.Level - 1).ToLocalizedLevelName()));
            }

            // valid
            else if (newPerm > Permission.Banned)
            {
                targetClient = targetClient.ClientNumber < 0
                    ? gameEvent.Owner.Manager.GetActiveClients()
                        .FirstOrDefault(c => c.ClientId == targetClient?.ClientId) ?? targetClient
                    : targetClient;

                logger.LogDebug("Beginning set level of client {origin} to {newPermission}",
                    gameEvent.Origin.ToString(), newPerm);

                var result = await targetClient.SetLevel(newPerm, gameEvent.Origin)
                    .WaitAsync(Utilities.DefaultCommandTimeout, gameEvent.Owner.Manager.CancellationToken);

                if (result.Failed)
                {
                    // user is the same level
                    if (result.FailReason == GameEvent.EventFailReason.Invalid)
                    {
                        gameEvent.Origin.Tell(_translationLookup["COMMANDS_SETLEVEL_INVALID"]
                            .FormatExt(gameEvent.Target.Name + "(Color::White)", newPerm.ToString()));
                        return;
                    }

                    using (LogContext.PushProperty("Server", gameEvent.Origin.CurrentServer?.ToString()))
                    {
                        logger.LogWarning("Failed to set level of client {origin} {reason}",
                            gameEvent.Origin.ToString(),
                            result.FailReason);
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
                _ = newPerm < oldPerm
                    ? gameEvent.Origin.Tell(_translationLookup["COMMANDS_SETLEVEL_DEMOTE_SUCCESS"]
                        .FormatExt(targetClient.Name))
                    : gameEvent.Origin.Tell(
                        _translationLookup["COMMANDS_SETLEVEL_SUCCESS"].FormatExt(targetClient.Name));
            }

            // all other tests failed so it's invalid group
            else
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_SETLEVEL_FAIL"]);
            }
        }
    }

    /// <summary>
    ///     Prints the amount of memory IW4MAdmin is using
    /// </summary>
    public class MemoryUsageCommand : Command
    {
        public MemoryUsageCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "usage";
            Description = _translationLookup["COMMANDS_USAGE_DESC"];
            Alias = "us";
            Permission = Permission.Moderator;
            RequiresTarget = false;
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            E.Origin.Tell(_translationLookup["COMMANDS_USAGE_TEXT"]
                .FormatExt(Math.Round(Process.GetCurrentProcess().PrivateMemorySize64 / 2048f / 1200f, 1)));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    ///     Prints out how long IW4MAdmin has been running
    /// </summary>
    public class UptimeCommand : Command
    {
        public UptimeCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "uptime";
            Description = _translationLookup["COMMANDS_UPTIME_DESC"];
            Alias = "up";
            Permission = Permission.Moderator;
            RequiresTarget = false;
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            var uptime = DateTime.Now - Process.GetCurrentProcess().StartTime;
            var loc = _translationLookup;
            E.Origin.Tell(loc["COMMANDS_UPTIME_TEXT"].FormatExt(uptime.HumanizeForCurrentCulture(4)));
            return Task.CompletedTask;
        }
    }


    /// <summary>
    ///     Attempts to load the specified map
    /// </summary>
    public class LoadMapCommand : Command
    {
        public LoadMapCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "map";
            Description = _translationLookup["COMMANDS_MAP_DESC"];
            Alias = "m";
            Permission = Permission.Administrator;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_MAP"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var newMap = E.Data.Trim();
            var delay = E.Owner.Manager.GetApplicationSettings().Configuration().MapChangeDelaySeconds * 1000;

            var foundMap = E.Owner.Maps.FirstOrDefault(_map =>
                _map.Name.Equals(newMap, StringComparison.InvariantCultureIgnoreCase) ||
                _map.Alias.Equals(newMap, StringComparison.InvariantCultureIgnoreCase));

            _ = foundMap == null
                ? E.Owner.Broadcast(_translationLookup["COMMANDS_MAP_UKN"].FormatExt(newMap))
                : E.Owner.Broadcast(_translationLookup["COMMANDS_MAP_SUCCESS"].FormatExt(foundMap.Alias));

            await Task.Delay(delay);
            await E.Owner.LoadMap(foundMap?.Name ?? newMap);
        }
    }


    /// <summary>
    ///     Lists server and global rules
    /// </summary>
    public class ListRulesCommands : Command
    {
        public ListRulesCommands(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "rules";
            Description = _translationLookup["COMMANDS_RULES_DESC"];
            Alias = "r";
            Permission = Permission.User;
            RequiresTarget = false;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            if (gameEvent.Owner.Manager.GetApplicationSettings().Configuration().GlobalRules?.Length < 1 &&
                gameEvent.Owner.ServerConfig.Rules?.Length < 1)
            {
                var _ = gameEvent.Message.IsBroadcastCommand(_config.BroadcastCommandPrefix)
                    ? gameEvent.Owner.Broadcast(_translationLookup["COMMANDS_RULES_NONE"])
                    : gameEvent.Origin.Tell(_translationLookup["COMMANDS_RULES_NONE"]);
            }

            else
            {
                var rules = new List<string>();
                rules.AddRange(gameEvent.Owner.Manager.GetApplicationSettings().Configuration().GlobalRules);
                if (gameEvent.Owner.ServerConfig.Rules != null)
                {
                    rules.AddRange(gameEvent.Owner.ServerConfig.Rules);
                }

                var ruleFormat = rules.Select(r => $"- {r}");
                if (gameEvent.Message.IsBroadcastCommand(_config.BroadcastCommandPrefix))
                {
                    await gameEvent.Owner.BroadcastAsync(ruleFormat, token: gameEvent.Owner.Manager.CancellationToken);
                }
                else
                {
                    await gameEvent.Origin.TellAsync(ruleFormat, gameEvent.Owner.Manager.CancellationToken);
                }
            }
        }
    }


    /// <summary>
    ///     Flag given client for specified reason
    /// </summary>
    public class FlagClientCommand : Command
    {
        public FlagClientCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "flag";
            Description = _translationLookup["COMMANDS_FLAG_DESC"];
            Alias = "fp";
            Permission = Permission.Moderator;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            switch ((await E.Target.Flag(E.Data, E.Origin)
                        .WaitAsync(Utilities.DefaultCommandTimeout, E.Owner.Manager.CancellationToken)).FailReason)
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
    ///     Unflag given client for specified reason
    /// </summary>
    public class UnflagClientCommand : Command
    {
        public UnflagClientCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "unflag";
            Description = _translationLookup["COMMANDS_UNFLAG_DESC"];
            Alias = "uf";
            Permission = Permission.Moderator;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            switch ((await E.Target.Unflag(E.Data, E.Origin)
                        .WaitAsync(Utilities.DefaultCommandTimeout, E.Owner.Manager.CancellationToken)).FailReason)
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
    ///     Masks client from announcements and online admin list
    /// </summary>
    public class MaskCommand : Command
    {
        public MaskCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "mask";
            Description = _translationLookup["COMMANDS_MASK_DESC"];
            Alias = "ma";
            Permission = Permission.Moderator;
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
    ///     Lists ban information for given client
    /// </summary>
    public class ListBanInfoCommand : Command
    {
        public ListBanInfoCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "baninfo";
            Description = _translationLookup["COMMANDS_BANINFO_DESC"];
            Alias = "bi";
            Permission = Permission.Moderator;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var existingPenalties = await E.Owner.Manager.GetPenaltyService()
                .GetActivePenaltiesAsync(E.Target.AliasLinkId, E.Target.CurrentAliasId, E.Target.NetworkId, E.Target.GameName, E.Target.IPAddress);
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
                var remainingTime = (penalty.Expires.Value - DateTime.UtcNow).HumanizeForCurrentCulture();
                E.Origin.Tell(_translationLookup["COMMANDS_BANINFO_TB_SUCCESS"]
                    .FormatExt(E.Target.Name, penalty.Offense, remainingTime));
            }
        }
    }

    /// <summary>
    ///     Executes RCon command
    /// </summary>
    public class ExecuteRConCommand : Command
    {
        public ExecuteRConCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "rcon";
            Description = _translationLookup["COMMANDS_RCON_DESC"];
            Alias = "rcon";
            Permission = Permission.Owner;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_COMMANDS"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var response = await E.Owner.ExecuteCommandAsync(E.Data.Trim());
            foreach (var item in response)
                E.Origin.Tell(item);

            if (response.Length == 0)
            {
                E.Origin.Tell(_translationLookup["COMMANDS_RCON_SUCCESS"]);
            }
        }
    }

    /// <summary>
    ///     Lists external IP
    /// </summary>
    public class ListExternalIPCommand : Command
    {
        public ListExternalIPCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
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
    ///     Prunes inactive privileged clients
    /// </summary>
    public class PruneAdminsCommand : Command
    {
        private readonly IDatabaseContextFactory _contextFactory;

        public PruneAdminsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
            IDatabaseContextFactory contextFactory) : base(config, translationLookup)
        {
            Name = "prune";
            Description = _translationLookup["COMMANDS_PRUNE_DESC"];
            Alias = "pa";
            Permission = Permission.Owner;
            RequiresTarget = false;
            _contextFactory = contextFactory;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_INACTIVE"],
                    Required = false
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var inactiveDays = 30;

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
            await using var context = _contextFactory.CreateContext();
            var lastActive = DateTime.UtcNow.AddDays(-inactiveDays);
            inactiveUsers = await context.Clients
                .Where(c => c.Level > Permission.Flagged && c.Level <= Permission.Moderator)
                .Where(c => c.LastConnection < lastActive)
                .Select(c => c.ToPartialClient())
                .ToListAsync();
            inactiveUsers.ForEach(c => c.SetLevel(Permission.User, E.Origin));
            await context.SaveChangesAsync();

            E.Origin.Tell(_translationLookup["COMMANDS_PRUNE_SUCCESS"].FormatExt(inactiveUsers.Count));
        }
    }


    /// <summary>
    ///     Sets login password
    /// </summary>
    public class SetPasswordCommand : Command
    {
        public SetPasswordCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "setpassword";
            Description = _translationLookup["COMMANDS_SETPASSWORD_DESC"];
            Alias = "sp";
            Permission = Permission.Moderator;
            RequiresTarget = false;
            AllowImpersonation = true;
            Arguments = new[]
            {
                new CommandArgument
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

            var hashedPassword = Hashing.Hash(E.Data);

            E.Origin.Password = hashedPassword[0];
            E.Origin.PasswordSalt = hashedPassword[1];

            await E.Owner.Manager.GetClientService().Update(E.Origin);
            E.Origin.Tell(_translationLookup["COMMANDS_PASSWORD_SUCCESS"]);
        }
    }

    /// <summary>
    ///     Gets the ping of a client
    /// </summary>
    public class GetClientPingCommand : Command
    {
        public GetClientPingCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "ping";
            Description = _translationLookup["COMMANDS_PING_DESC"];
            Alias = "pi";
            Permission = Permission.User;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument
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
                E.Origin.Tell(_translationLookup["COMMANDS_PING_SELF_V2"].FormatExt(E.Origin.Ping));
            }
            else
            {
                E.Origin.Tell(_translationLookup["COMMANDS_PING_TARGET_V2"].FormatExt(E.Target.Name, E.Target.Ping));
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    ///     Sets the email for gravatar in webfront
    /// </summary>
    public class SetGravatarCommand : Command
    {
        private readonly IMetaServiceV2 _metaService;

        public SetGravatarCommand(CommandConfiguration config, ITranslationLookup translationLookup,
            IMetaServiceV2 metaService) : base(config, translationLookup)
        {
            Name = "setgravatar";
            Description = _translationLookup["COMMANDS_GRAVATAR_DESC"];
            Alias = "sg";
            Permission = Permission.User;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument
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
                var gravatarEmail = string.Concat(md5
                    .ComputeHash(E.Data.ToLower().Select(d => Convert.ToByte(d)).ToArray())
                    .Select(h => h.ToString("x2")));
                await _metaService.SetPersistentMeta("GravatarEmail", gravatarEmail, E.Origin.ClientId,
                    E.Owner.Manager.CancellationToken);
            }

            E.Origin.Tell(_translationLookup["COMMANDS_GRAVATAR_SUCCESS_NEW"]);
        }
    }

    /// <summary>
    ///     Retrieves the next map in rotation
    /// </summary>
    public class NextMapCommand : Command
    {
        public NextMapCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "nextmap";
            Description = _translationLookup["COMMANDS_NEXTMAP_DESC"];
            Alias = "nm";
            Permission = Permission.User;
            RequiresTarget = false;
        }

        public static async Task<string> GetNextMap(Server s, ITranslationLookup lookup)
        {
            var mapRotation = (await s.GetDvarAsync<string>("sv_mapRotation", token: s.Manager.CancellationToken)).Value?.ToLower() ?? "";
            var regexMatches = Regex.Matches(mapRotation,
                    @"((?:gametype|exec) +(?:([a-z]{1,4})(?:.cfg)?))? *map ([a-z|_|\d]+)", RegexOptions.IgnoreCase)
                .ToList();

            // find the current map in the rotation
            var currentMap = regexMatches.Where(m => m.Groups[3].ToString() == s.CurrentMap.Name);
            var lastMap = regexMatches.LastOrDefault();
            Map nextMap = null;

            // no maprotation at all
            if (regexMatches.Count() == 0)
            {
                return lookup["COMMANDS_NEXTMAP_SUCCESS"]
                    .FormatExt(s.CurrentMap.Alias, Utilities.GetLocalizedGametype(s.Gametype));
            }

            // the current map is not in rotation
            if (currentMap.Count() == 0)
            {
                return lookup["COMMANDS_NEXTMAP_NOT_IN_ROTATION"];
            }

            // there's duplicate maps in rotation

            if (currentMap.Count() > 1)
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
            var nextMapMatch = currentMap.First().Index != lastMap.Index
                ? regexMatches[regexMatches.IndexOf(currentMap.First()) + 1]
                : regexMatches.First();

            var nextMapName = nextMapMatch.Groups[3].ToString();

            nextMap = s.Maps.FirstOrDefault(m => m.Name == nextMapMatch.Groups[3].ToString()) ??
                      new Map { Alias = nextMapName, Name = nextMapName };
            var nextGametype = nextMapMatch.Groups[2].ToString().Length == 0
                ? Utilities.GetLocalizedGametype(s.Gametype)
                : Utilities.GetLocalizedGametype(nextMapMatch.Groups[2].ToString());

            return lookup["COMMANDS_NEXTMAP_SUCCESS"].FormatExt(nextMap.Alias, nextGametype);
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            E.Origin.Tell(await GetNextMap(E.Owner, _translationLookup));
        }
    }
}
