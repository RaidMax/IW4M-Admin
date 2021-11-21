using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands
{
    /// <summary>
    /// Prints help information
    /// </summary>
    public class HelpCommand : Command
    {
        public HelpCommand(CommandConfiguration config, ITranslationLookup translationLookup) :
            base(config, translationLookup)
        {
            Name = "help";
            Description = translationLookup["COMMANDS_HELP_DESC"];
            Alias = "h";
            Permission = EFClient.Permission.User;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = translationLookup["COMMANDS_ARGS_COMMANDS"],
                    Required = false
                }
            };
        }

        public override Task ExecuteAsync(GameEvent gameEvent)
        {
            var searchTerm = gameEvent.Data.Trim();
            var availableCommands = gameEvent.Owner.Manager.Commands.Distinct().Where(command =>
                    command.SupportedGames == null || !command.SupportedGames.Any() ||
                    command.SupportedGames.Contains(gameEvent.Owner.GameName))
                .Where(command => gameEvent.Origin.Level >= command.Permission);

            if (searchTerm.Length > 2)
            {
                var matchingCommand = availableCommands.FirstOrDefault(command =>
                    command.Name.Equals(searchTerm, StringComparison.InvariantCultureIgnoreCase) ||
                    command.Alias.Equals(searchTerm, StringComparison.InvariantCultureIgnoreCase));

                if (matchingCommand != null)
                {
                    gameEvent.Origin.Tell(_translationLookup["COMMANDS_HELP_SEARCH_RESULT"]
                        .FormatExt(matchingCommand.Name, matchingCommand.Alias));
                    gameEvent.Origin.Tell(matchingCommand.Syntax);
                }

                else
                {
                    gameEvent.Origin.Tell(_translationLookup["COMMANDS_HELP_NOTFOUND"]);
                }
            }

            else
            {
                var commandStrings = availableCommands.Select((command, index) =>
                    new
                    {
                        response = $" {_translationLookup["COMMANDS_HELP_LIST_FORMAT"].FormatExt(command.Name)} ",
                        index
                    });

                var helpResponse = new StringBuilder();

                foreach (var item in commandStrings)
                {
                    helpResponse.Append(item.response);
                    if (item.index == 0 || item.index % 4 != 0)
                    {
                        continue;
                    }

                    gameEvent.Origin.Tell(helpResponse.ToString());
                    helpResponse = new StringBuilder();
                }

                gameEvent.Origin.Tell(helpResponse.ToString());
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_HELP_MOREINFO"]);
            }

            return Task.CompletedTask;
        }
    }
}