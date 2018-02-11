using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Database
{
    public class Initializer : DropCreateDatabaseIfModelChanges<DatabaseContext>
    {
        protected override void Seed(DatabaseContext context)
        {
            var aliasLink = new Models.EFAliasLink();

            var currentAlias = new Models.EFAlias()
            {
                Active = true,
                DateAdded = DateTime.UtcNow,
                IPAddress = 0,
                Name = "IW4MAdmin",
                Link = aliasLink
            };

            context.Clients.Add(new Models.EFClient()
            {
                Active = false,
                Connections = 0,
                FirstConnection = DateTime.UtcNow,
                LastConnection = DateTime.UtcNow,
                Level = Objects.Player.Permission.Console,
                Masked = true,
                NetworkId = 0,
                AliasLink = aliasLink,
                CurrentAlias = currentAlias
            });

            base.Seed(context);
        }

    }
}
