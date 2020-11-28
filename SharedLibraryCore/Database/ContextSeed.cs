using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore.Interfaces;
using static SharedLibraryCore.Database.Models.EFClient;

namespace SharedLibraryCore.Database
{
    public static class ContextSeed
    {
        public static async Task Seed(IDatabaseContextFactory contextFactory, CancellationToken token)
        {
            var context = contextFactory.CreateContext();
            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await context.Database.MigrateAsync(token);
            });

            if (!await context.AliasLinks.AnyAsync(token))
            {
                var link = new EFAliasLink();

                context.Clients.Add(new EFClient()
                {
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

                await context.SaveChangesAsync(token);
            }
        }
    }
}