using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Exceptions;

namespace SharedLibraryCore.Commands
{
    public class CommandProcessing
    {
        public static async Task<Command> ValidateCommand(GameEvent gameEvent, ApplicationConfiguration appConfig,
            CommandConfiguration commandConfig)
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;
            var manager = gameEvent.Owner.Manager;
            var isBroadcast = gameEvent.Data.StartsWith(appConfig.BroadcastCommandPrefix);
            var prefixLength = isBroadcast ? appConfig.BroadcastCommandPrefix.Length : appConfig.CommandPrefix.Length;

            var commandString =
                gameEvent.Data.Substring(prefixLength, gameEvent.Data.Length - prefixLength).Split(' ')[0];
            gameEvent.Message = gameEvent.Data;

            Command matchedCommand = null;
            foreach (var availableCommand in manager.GetCommands()
                         .Where(c => c.Name != null))
            {
                if ((availableCommand.SupportedGames?.Any() ?? false) &&
                    !availableCommand.SupportedGames.Contains(gameEvent.Owner.GameName))
                {
                    continue;
                }

                if (availableCommand.Name.Equals(commandString, StringComparison.OrdinalIgnoreCase) ||
                    (availableCommand.Alias ?? "").Equals(commandString, StringComparison.OrdinalIgnoreCase))
                {
                    matchedCommand = (Command)availableCommand;
                }
            }

            if (matchedCommand == null)
            {
                gameEvent.Origin.Tell(loc["COMMAND_UNKNOWN"]);
                throw new CommandException($"{gameEvent.Origin} entered unknown command \"{commandString}\"");
            }

            matchedCommand.IsBroadcast = isBroadcast;

            var allowImpersonation = commandConfig?.Commands?.ContainsKey(matchedCommand.GetType().Name) ?? false
                ? commandConfig.Commands[matchedCommand.GetType().Name].AllowImpersonation
                : matchedCommand.AllowImpersonation;

            if (!allowImpersonation && gameEvent.ImpersonationOrigin != null)
            {
                gameEvent.ImpersonationOrigin.Tell(loc["COMMANDS_RUN_AS_FAIL"]);
                throw new CommandException($"Command {matchedCommand.Name} cannot be run as another client");
            }

            gameEvent.Data = gameEvent.Data.RemoveWords(1);
            var args = gameEvent.Data.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // todo: the code below can be cleaned up 
            if (gameEvent.Origin.Level < matchedCommand.Permission)
            {
                gameEvent.Origin.Tell(loc["COMMAND_NOACCESS"]);
                throw new CommandException($"{gameEvent.Origin} does not have access to \"{matchedCommand.Name}\"");
            }

            if (args.Length < matchedCommand.RequiredArgumentCount)
            {
                gameEvent.Origin.Tell(loc["COMMAND_MISSINGARGS"]);
                gameEvent.Origin.Tell(matchedCommand.Syntax);
                throw new CommandException(
                    $"{gameEvent.Origin} did not supply enough arguments for \"{matchedCommand.Name}\"");
            }

            if (matchedCommand.RequiresTarget)
            {
                if (args.Length > 0)
                {
                    if (!int.TryParse(args[0], out var cNum))
                    {
                        cNum = -1;
                    }

                    if (args[0][0] == '@') // user specifying target by database ID
                    {
                        int.TryParse(args[0].Substring(1, args[0].Length - 1), out var dbID);

                        var found = await manager.GetClientService().Get(dbID);
                        if (found != null)
                        {
                            found = manager.FindActiveClient(found);
                            gameEvent.Target = found;
                            gameEvent.Target.CurrentServer = found.CurrentServer ?? gameEvent.Owner;
                            gameEvent.Data = string.Join(" ", args.Skip(1));
                        }
                    }

                    else if (args[0].Length < 3 && cNum > -1 && cNum < gameEvent.Owner.MaxClients
                            ) // user specifying target by client num
                    {
                        if (gameEvent.Owner.Clients[cNum] != null)
                        {
                            gameEvent.Target = gameEvent.Owner.Clients[cNum];
                            gameEvent.Data = string.Join(" ", args.Skip(1));
                        }
                    }
                }

                List<EFClient> matchingPlayers;

                if (gameEvent.Target == null &&
                    matchedCommand.RequiresTarget) // Find active player including quotes (multiple words)
                {
                    matchingPlayers = gameEvent.Owner.GetClientByName(gameEvent.Data);
                    if (matchingPlayers.Count > 1)
                    {
                        gameEvent.Origin.Tell(loc["COMMAND_TARGET_MULTI"]);
                        throw new CommandException(
                            $"{gameEvent.Origin} had multiple players found for {matchedCommand.Name}");
                    }

                    if (matchingPlayers.Count == 1)
                    {
                        gameEvent.Target = matchingPlayers.First();

                        var escapedName = Regex.Escape(gameEvent.Target.CleanedName);
                        var reg = new Regex($"(\"{escapedName}\")|({escapedName})", RegexOptions.IgnoreCase);
                        gameEvent.Data = reg.Replace(gameEvent.Data, "", 1).Trim();

                        if (gameEvent.Data.Length == 0 && matchedCommand.RequiredArgumentCount > 1)
                        {
                            gameEvent.Origin.Tell(loc["COMMAND_MISSINGARGS"]);
                            gameEvent.Origin.Tell(matchedCommand.Syntax);
                            throw new CommandException(
                                $"{gameEvent.Origin} did not supply enough arguments for \"{matchedCommand.Name}\"");
                        }
                    }
                }

                if (gameEvent.Target == null && matchedCommand.RequiresTarget &&
                    args.Length > 0) // Find active player as single word
                {
                    matchingPlayers = gameEvent.Owner.GetClientByName(args[0]);
                    if (matchingPlayers.Count > 1)
                    {
                        gameEvent.Origin.Tell(loc["COMMAND_TARGET_MULTI"]);
                        foreach (var p in matchingPlayers)
                            gameEvent.Origin.Tell($"[(Color::Yellow){p.ClientNumber}(Color::White)] {p.Name}");
                        throw new CommandException(
                            $"{gameEvent.Origin} had multiple players found for {matchedCommand.Name}");
                    }

                    if (matchingPlayers.Count == 1)
                    {
                        gameEvent.Target = matchingPlayers.First();

                        var escapedName = Regex.Escape(gameEvent.Target.CleanedName);
                        var escapedArg = Regex.Escape(args[0]);
                        var reg = new Regex($"({escapedName})|({escapedArg})", RegexOptions.IgnoreCase);
                        gameEvent.Data = reg.Replace(gameEvent.Data, "", 1).Trim();

                        if ((gameEvent.Data.Trim() == gameEvent.Target.CleanedName.ToLower().Trim() ||
                             gameEvent.Data == string.Empty) &&
                            matchedCommand.RequiresTarget)
                        {
                            gameEvent.Origin.Tell(loc["COMMAND_MISSINGARGS"]);
                            gameEvent.Origin.Tell(matchedCommand.Syntax);
                            throw new CommandException(
                                $"{gameEvent.Origin} did not supply enough arguments for \"{matchedCommand.Name}\"");
                        }
                    }
                }

                if (gameEvent.Target == null && matchedCommand.RequiresTarget)
                {
                    gameEvent.Origin.Tell(loc["COMMAND_TARGET_NOTFOUND"]);
                    throw new CommandException(
                        $"{gameEvent.Origin} specified invalid player for \"{matchedCommand.Name}\"");
                }
            }

            gameEvent.Data = gameEvent.Data.Trim();
            return matchedCommand;
        }
    }
}
