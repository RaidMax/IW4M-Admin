using System;
using System.Linq;
using System.Threading.Tasks;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands
{
    /// <summary>
    /// Lists all unmasked admins
    /// </summary>
    public class ListAdminsCommand : Command
    {
        public ListAdminsCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "admins";
            Description = _translationLookup["COMMANDS_ADMINS_DESC"];
            Alias = "a";
            Permission = EFClient.Permission.User;
            RequiresTarget = false;
        }

        public static string OnlineAdmins(Server server, ITranslationLookup lookup)
        {
            var onlineAdmins = server.GetClientsAsList()
                .Where(p => p.Level > EFClient.Permission.Flagged)
                .Where(p => !p.Masked)
                .Select(p =>
                    $"[(Color::Yellow){Utilities.ConvertLevelToColor(p.Level, p.ClientPermission.Name)}(Color::White)] {p.Name}")
                .ToList();

            return onlineAdmins.Any() ? string.Join(Environment.NewLine, onlineAdmins) : lookup["COMMANDS_ADMINS_NONE"];
        }

        public override Task ExecuteAsync(GameEvent gameEvent)
        {
            foreach (var line in OnlineAdmins(gameEvent.Owner, _translationLookup).Split(Environment.NewLine))
            {
                var _ = gameEvent.Message.IsBroadcastCommand(_config.BroadcastCommandPrefix)
                    ? gameEvent.Owner.Broadcast(line)
                    : gameEvent.Origin.Tell(line);
            }

            return Task.CompletedTask;
        }
    }
}