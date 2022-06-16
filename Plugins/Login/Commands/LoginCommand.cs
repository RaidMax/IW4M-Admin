using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System.Threading.Tasks;
using SharedLibraryCore.Helpers;

namespace IW4MAdmin.Plugins.Login.Commands
{
    public class LoginCommand : Command
    {
        public LoginCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
        {
            Name = "login";
            Description = _translationLookup["PLUGINS_LOGIN_COMMANDS_LOGIN_DESC"];
            Alias = "li";
            Permission = EFClient.Permission.Trusted;
            RequiresTarget = false;
            Arguments = new CommandArgument[]
            {
                new()
                {
                    Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PASSWORD"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            var success = gameEvent.Owner.Manager.TokenAuthenticator.AuthorizeToken(new TokenIdentifier
            {
                NetworkId = gameEvent.Origin.NetworkId,
                Game = gameEvent.Origin.GameName,
                Token = gameEvent.Data
            });

            if (!success)
            {
                var hashedPassword = await Task.FromResult(Hashing.Hash(gameEvent.Data, gameEvent.Origin.PasswordSalt));
                success = hashedPassword[0] == gameEvent.Origin.Password;
            }

            if (success)
            {
                Plugin.AuthorizedClients[gameEvent.Origin.ClientId] = true;
            }

            _ = success ?
                gameEvent.Origin.Tell(_translationLookup["PLUGINS_LOGIN_COMMANDS_LOGIN_SUCCESS"]) :
                gameEvent.Origin.Tell(_translationLookup["PLUGINS_LOGIN_COMMANDS_LOGIN_FAIL"]);
        }
    }
}
