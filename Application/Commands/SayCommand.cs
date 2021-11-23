using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace IW4MAdmin.Application.Commands
{
    /// <summary>
    /// Prints out a message to all clients on the server
    /// </summary>
    public class SayCommand : Command
    {
        public SayCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "say";
            Description = _translationLookup["COMMANDS_SAY_DESC"];
            Alias = "s";
            Permission = EFClient.Permission.Moderator;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_MESSAGE"],
                    Required = true
                }
            };
        }

        public override Task ExecuteAsync(GameEvent gameEvent)
        {
            gameEvent.Owner.Broadcast(
                _translationLookup["COMMANDS_SAY_FORMAT"].FormatExt(gameEvent.Origin.Name, gameEvent.Data),
                gameEvent.Origin);
            gameEvent.Origin.Tell(_translationLookup["COMMANDS_SAY_SUCCESS"]);
            return Task.CompletedTask;
        }
    }
}