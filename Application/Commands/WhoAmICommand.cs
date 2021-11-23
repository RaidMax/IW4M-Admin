using System.Threading.Tasks;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands
{
    /// <summary>
    /// Prints client information
    /// </summary>
    public class WhoAmICommand : Command
    {
        public WhoAmICommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "whoami";
            Description = _translationLookup["COMMANDS_WHO_DESC"];
            Alias = "who";
            Permission = EFClient.Permission.User;
            RequiresTarget = false;
        }

        public override Task ExecuteAsync(GameEvent gameEvent)
        {
            var you =
                "[(Color::Yellow)#{{clientNumber}}(Color::White)] [(Color::Yellow)@{{clientId}}(Color::White)] [{{networkId}}] [{{ip}}] [(Color::Cyan){{level}}(Color::White){{tag}}(Color::White)] {{name}}"
                    .FormatExt(gameEvent.Origin.ClientNumber,
                        gameEvent.Origin.ClientId, gameEvent.Origin.GuidString,
                        gameEvent.Origin.IPAddressString, gameEvent.Origin.ClientPermission.Name,
                        string.IsNullOrEmpty(gameEvent.Origin.Tag) ? "" : $" {gameEvent.Origin.Tag}",
                        gameEvent.Origin.Name);
            gameEvent.Origin.Tell(you);

            return Task.CompletedTask;
        }
    }
}