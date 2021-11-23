using System.Threading.Tasks;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands
{
    /// <summary>
    /// Prints out a message to all clients on all servers
    /// </summary>
    public class SayAllCommand : Command
    {
        public SayAllCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "sayall";
            Description = _translationLookup["COMMANDS_SAY_ALL_DESC"];
            Alias = "sa";
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
            var message = $"(Color::Accent){gameEvent.Origin.Name}(Color::White) - (Color::Red){gameEvent.Data}";

            foreach (var server in gameEvent.Owner.Manager.GetServers())
            {
                server.Broadcast(message, gameEvent.Origin);
            }

            gameEvent.Origin.Tell(_translationLookup["COMMANDS_SAY_SUCCESS"]);
            return Task.CompletedTask;
        }
    }
}