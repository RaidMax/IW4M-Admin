using System;
using System.Linq;
using System.Threading.Tasks;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands
{
    /// <summary>
    /// Finds player by name
    /// </summary>
    public class FindPlayerCommand : Command
    {
        public FindPlayerCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "find";
            Description = _translationLookup["COMMANDS_FIND_DESC"];
            Alias = "f";
            Permission = EFClient.Permission.Administrator;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            if (gameEvent.Data.Length < 3)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_FIND_MIN"]);
                return;
            }

            var players = await gameEvent.Owner.Manager.GetClientService().FindClientsByIdentifier(gameEvent.Data);

            if (!players.Any())
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_FIND_EMPTY"]);
                return;
            }

            foreach (var client in players)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_FIND_FORMAT_V2"].FormatExt(client.Name,
                    client.ClientId, Utilities.ConvertLevelToColor((EFClient.Permission) client.LevelInt, client.Level),
                    client.IPAddress, (DateTime.UtcNow - client.LastConnection).HumanizeForCurrentCulture()));
            }
        }
    }
}