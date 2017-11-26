using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Database
{
    public class Initializer : DropCreateDatabaseIfModelChanges<IW4MAdminDatabaseContext>
    {
        protected override void Seed(IW4MAdminDatabaseContext context)
        {
            context.Clients.Add(new Models.EFClient()
            {
                Active = false,
                Connections = 0,
                AliasLink = new Models.EFAliasLink(),
                FirstConnection = DateTime.UtcNow,
                IPAddress = "127.0.0.1",
                LastConnection = DateTime.UtcNow,
                Level = Objects.Player.Permission.Console,
                Name = "IW4MAdmin",
                Masked = true,
                NetworkId = "0000000000000000",
            });

            base.Seed(context);
        }

    }
}
