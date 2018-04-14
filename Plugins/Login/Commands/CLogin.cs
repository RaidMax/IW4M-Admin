using SharedLibraryCore;
using SharedLibraryCore.Objects;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IW4MAdmin.Plugins.Login.Commands
{
    public class CLogin : Command
    {
        public CLogin() : base("login", "login using password", "l", Player.Permission.Trusted, false, new CommandArgument[]
        {
            new CommandArgument()
            {
                Name = "password",
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
                await E.Origin.Tell("You are now logged in");
            }

            else
            {
                await E.Origin.Tell("Your password is incorrect");
            }
        }
    }
}
