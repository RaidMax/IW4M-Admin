using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using static SharedLibraryCore.Database.Models.EFClient;

namespace SharedLibraryCore.Database
{
    public class ContextSeed
    {
        private DatabaseContext context;

        public ContextSeed(DatabaseContext ctx)
        {
            context = ctx;
        }

        public async Task Seed()
        {
            // make sure database exists
            //context.Database.EnsureCreated();
            context.Database.Migrate();

            if (context.AliasLinks.Count() == 0)
            {
                context.AliasLinks.Add(new EFAliasLink()
                {
                    AliasLinkId = 1
                });

                var currentAlias = new EFAlias()
                {
                    AliasId = 1,
                    Active = true,
                    DateAdded = DateTime.UtcNow,
                    Name = "IW4MAdmin",
                    LinkId = 1
                };

                context.Aliases.Add(currentAlias);

                context.Clients.Add(new EFClient()
                {
                    ClientId = 1,
                    Active = false,
                    Connections = 0,
                    FirstConnection = DateTime.UtcNow,
                    LastConnection = DateTime.UtcNow,
                    Level = Permission.Console,
                    Masked = true,
                    NetworkId = 0,
                    AliasLinkId = 1,
                    CurrentAliasId = 1,
                });

                await context.SaveChangesAsync();
            }
        }
    }
}
