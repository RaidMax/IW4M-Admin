using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using System.Threading.Tasks;

namespace IW4MAdmin.Plugins.Login.Commands
{
    public class CLogin : Command
    {
        public CLogin() : base("login", Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_LOGIN_COMMANDS_LOGIN_DESC"], "li", EFClient.Permission.Trusted, false, new CommandArgument[]
        {
            new CommandArgument()
            {
                Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PASSWORD"],
                Required = true
            }
        })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var client = E.Owner.Manager.PrivilegedClients[E.Origin.ClientId];
            bool success = E.Owner.Manager.TokenAuthenticator.AuthorizeToken(E.Origin.NetworkId, E.Data);

            if (!success)
            {
                string[] hashedPassword = await Task.FromResult(SharedLibraryCore.Helpers.Hashing.Hash(E.Data, client.PasswordSalt));
                success = hashedPassword[0] == client.Password;
            }

            if (success)
            {
                Plugin.AuthorizedClients[E.Origin.ClientId] = true;
            }

            _ = success ?
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_LOGIN_COMMANDS_LOGIN_SUCCESS"]) :
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_LOGIN_COMMANDS_LOGIN_FAIL"]);
        }
    }
}
