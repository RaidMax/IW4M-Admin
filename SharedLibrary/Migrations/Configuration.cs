namespace SharedLibrary.Migrations
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

    internal sealed class Configuration : DbMigrationsConfiguration<SharedLibrary.Database.DatabaseContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = true;
            if (Utilities.IsRunningOnMono())
            {
                SetSqlGenerator("MySql.Data.MySqlClient", new MySql.Data.Entity.MySqlMigrationSqlGenerator());
            }
        }

        protected override void Seed(SharedLibrary.Database.DatabaseContext context)
        {
            context.AliasLinks.AddOrUpdate(new SharedLibrary.Database.Models.EFAliasLink()
            {
                AliasLinkId = 1
            });

            var currentAlias = new SharedLibrary.Database.Models.EFAlias()
            {
                AliasId = 1,
                Active = true,
                DateAdded = DateTime.UtcNow,
                IPAddress = 0,
                Name = "IW4MAdmin",
                LinkId = 1
            };

            context.Aliases.AddOrUpdate(currentAlias);

            context.Clients.AddOrUpdate(new SharedLibrary.Database.Models.EFClient()
            {
                ClientId = 1,
                Active = false,
                Connections = 0,
                FirstConnection = DateTime.UtcNow,
                LastConnection = DateTime.UtcNow,
                Level = Objects.Player.Permission.Console,
                Masked = true,
                NetworkId = 0,
                AliasLinkId = 1,
                CurrentAliasId = 1,
            });

            base.Seed(context);
        }
    }
}
