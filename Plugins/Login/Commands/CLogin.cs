using SharedLibraryCore;
using SharedLibraryCore.Objects;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IW4MAdmin.Plugins.Login.Commands
{
    public class CLogin : Command
    {
        public CLogin() : base("login", Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_LOGIN_COMMANDS_LOGIN_DESC"], "li", Player.Permission.Trusted, false, new CommandArgument[]
        {
            new CommandArgument()
            {
                Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PASSWORD"],
                Required = true
            }
        }){ }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var client = E.Owner.Manager.GetPrivilegedClients()[E.Origin.ClientId];
            string[] hashedPassword = await Task.FromResult(SharedLibraryCore.Helpers.Hashing.Hash(E.Data, client.PasswordSalt));

            if (hashedPassword[0] == client.Password)
            {
                Plugin.AuthorizedClients[E.Origin.ClientId] = true;
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_LOGIN_COMMANDS_LOGIN_SUCCESS"]);
            }

            else
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_LOGIN_COMMANDS_LOGIN_FAIL"]);
            }
        }
    }
}
