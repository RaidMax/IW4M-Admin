using System.Linq;
using System.Threading.Tasks;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands
{
    /// <summary>
    /// List online clients
    /// </summary>
    public class ListClientsCommand : Command
    {
        public ListClientsCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "list";
            Description = _translationLookup["COMMANDS_LIST_DESC"];
            Alias = "l";
            Permission = EFClient.Permission.Moderator;
            RequiresTarget = false;
        }

        public override Task ExecuteAsync(GameEvent gameEvent)
        {
            var clientList = gameEvent.Owner.GetClientsAsList()
                .Select(client =>
                    $"[(Color::Accent){client.ClientPermission.Name}(Color::White){(string.IsNullOrEmpty(client.Tag) ? "" : $" {client.Tag}")}(Color::White)][(Color::Yellow)#{client.ClientNumber}(Color::White)] {client.Name}")
                .ToArray();

            gameEvent.Origin.Tell(clientList);

            return Task.CompletedTask;
        }
    }
}