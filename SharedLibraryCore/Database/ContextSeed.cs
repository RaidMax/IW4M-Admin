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
            context.Database.Migrate();

            if (context.AliasLinks.Count() == 0)
            {
                var link = new EFAliasLink();

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
                    AliasLink = link,
                    CurrentAlias = new EFAlias()
                    {
                        Link = link,
                        Active = true,
                        DateAdded = DateTime.UtcNow,
                        Name = "IW4MAdmin",
                    },
                });

                await context.SaveChangesAsync();
            }
        }
    }
}
