using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;
using static SharedLibraryCore.Server;

namespace SharedLibraryCore.Commands
{
    public class PrivateMessageAdminsCommand : Command
    {
        public PrivateMessageAdminsCommand(CommandConfiguration config, ITranslationLookup lookup) : base(config, lookup)
        {
            Name = "privatemessageadmin";
            Description = lookup["COMMANDS_PMADMINS_DESC"];
            Alias = "pma";
            Permission = EFClient.Permission.Moderator;
            SupportedGames = new[] { Game.IW4, Game.IW5 };
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            bool isGameSupported = _config.Commands[nameof(PrivateMessageAdminsCommand)].SupportedGames.Length > 0 &&
                _config.Commands[nameof(PrivateMessageAdminsCommand)].SupportedGames.Contains(E.Owner.GameName);

            if (!isGameSupported)
            {
                E.Origin.Tell(_translationLookup["COMMANDS_GAME_NOT_SUPPORTED"].FormatExt(nameof(PrivateMessageAdminsCommand)));
                return Task.CompletedTask;
            }

            E.Owner.ToAdmins(E.Data);
            return Task.CompletedTask;
        }
    }
}
