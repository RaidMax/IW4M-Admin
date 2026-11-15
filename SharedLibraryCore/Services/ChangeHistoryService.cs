using System;
using System.Collections.Generic;
using System.Linq;
using SharedLibraryCore.Commands;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Data.Models.Client;
using Microsoft.Extensions.Logging;

namespace SharedLibraryCore.Services
{
    public class ChangeHistoryService
    {
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly ILogger _logger;

        public ChangeHistoryService(ILogger<ChangeHistoryService> logger, IDatabaseContextFactory contextFactory)
        {
            _logger = logger;
            _contextFactory = contextFactory;
        }

        /// <summary>names and aliases of commands whose arguments are a password or a login token</summary>
        private static readonly HashSet<string> SecretArgumentCommands =
            new(StringComparer.OrdinalIgnoreCase) { "login", "li", "setpassword", "sp" };

        /// <summary>
        ///     masks the arguments of a command that carries a secret, including when that command is wrapped
        ///     by another one (e.g. "!runas @5 setpassword hunter22": everything after the inner command is masked)
        /// </summary>
        private static string MaskSecrets(Command cmd, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return message;
            }

            var parts = message.Split(' ');
            var firstSecretIndex = -1;

            if (SecretArgumentCommands.Contains(cmd.Name))
            {
                firstSecretIndex = 1;
            }
            else if (cmd is RunAsCommand)
            {
                // "{prefix}runas {target} {command} {arguments}": find the wrapped command
                for (var index = 1; index < parts.Length; index++)
                {
                    var word = parts[index].TrimStart(Utilities.CommandPrefix);

                    if (SecretArgumentCommands.Contains(word))
                    {
                        firstSecretIndex = index + 1;
                        break;
                    }
                }
            }

            if (firstSecretIndex < 0)
            {
                return message;
            }

            return string.Join(' ', parts.Select((arg, index) => index >= firstSecretIndex ? "*****" : arg));
        }

        public async Task Add(GameEvent e)
        {
            EFChangeHistory change = null;

            switch (e.Type)
            {
                case GameEvent.EventType.Ban:
                    change = new EFChangeHistory
                    {
                        OriginEntityId = e.Origin.ClientId,
                        TargetEntityId = e.Target.ClientId,
                        ImpersonationEntityId = e.ImpersonationOrigin?.ClientId,
                        TypeOfChange = EFChangeHistory.ChangeType.Ban,
                        Comment = e.Data
                    };
                    break;
                case GameEvent.EventType.Command:
                    // this prevents passwords/tokens being logged into the database in plain text
                    if (e.Extra is Command cmd)
                    {
                        e.Message = MaskSecrets(cmd, e.Message);
                    }

                    change = new EFChangeHistory
                    {
                        OriginEntityId = e.Origin.ClientId,
                        TargetEntityId = e.Target?.ClientId ?? 0,
                        ImpersonationEntityId = e.ImpersonationOrigin?.ClientId,
                        Comment = "Executed command",
                        CurrentValue = e.Message,
                        TypeOfChange = EFChangeHistory.ChangeType.Command
                    };
                    break;
                case GameEvent.EventType.ChangePermission:
                    change = new EFChangeHistory
                    {
                        OriginEntityId = e.Origin.ClientId,
                        TargetEntityId = e.Target.ClientId,
                        ImpersonationEntityId = e.ImpersonationOrigin?.ClientId,
                        Comment = "Changed permission level",
                        TypeOfChange = EFChangeHistory.ChangeType.Permission,
                        CurrentValue = ((EFClient.Permission)e.Extra).ToString()
                    };
                    break;
                case GameEvent.EventType.Login:
                    change = new EFChangeHistory
                    {
                        OriginEntityId = e.Origin.ClientId,
                        Comment = "Logged In To Webfront",
                        TypeOfChange = EFChangeHistory.ChangeType.Command,
                        CurrentValue = e.Data
                    };
                    break;
                case GameEvent.EventType.Logout:
                    change = new EFChangeHistory
                    {
                        OriginEntityId = e.Origin.ClientId,
                        Comment = "Logged Out of Webfront",
                        TypeOfChange = EFChangeHistory.ChangeType.Command,
                        CurrentValue = e.Data
                    };
                    break;
            }

            if (change == null)
            {
                return;
            }

            await using var context = _contextFactory.CreateContext(false);

            context.EFChangeHistory.Add(change);

            try
            {
                await context.SaveChangesAsync();
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not persist change @{change}", change);
            }
        }
    }
}