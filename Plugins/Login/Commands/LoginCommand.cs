using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System.Threading.Tasks;

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
                new CommandArgument()
                {
                    Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PASSWORD"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            bool success = E.Owner.Manager.TokenAuthenticator.AuthorizeToken(E.Origin.NetworkId, E.Data);

            if (!success)
            {
                string[] hashedPassword = await Task.FromResult(SharedLibraryCore.Helpers.Hashing.Hash(E.Data, E.Origin.PasswordSalt));
                success = hashedPassword[0] == E.Origin.Password;
            }

            if (success)
            {
                Plugin.AuthorizedClients[E.Origin.ClientId] = true;
            }

            _ = success ?
                E.Origin.Tell(_translationLookup["PLUGINS_LOGIN_COMMANDS_LOGIN_SUCCESS"]) :
                E.Origin.Tell(_translationLookup["PLUGINS_LOGIN_COMMANDS_LOGIN_FAIL"]);
        }
    }
}
