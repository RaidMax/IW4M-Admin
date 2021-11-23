using System.Threading.Tasks;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands
{
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
            Permission = EFClient.Permission.User;
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
                    Name = _translationLookup["COMMANDS_ARGS_MESSAGE"],
                    Required = true
                }
            };
        }

        public override Task ExecuteAsync(GameEvent gameEvent)
        {
            gameEvent.Target.Tell(_translationLookup["COMMANDS_PRIVATE_MESSAGE_FORMAT"].FormatExt(gameEvent.Origin.Name, gameEvent.Data));
            gameEvent.Origin.Tell(_translationLookup["COMMANDS_PRIVATE_MESSAGE_RESULT"]
                .FormatExt(gameEvent.Target.Name, gameEvent.Data));
            return Task.CompletedTask;
        }
    }
}