using System;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Data.Models.Client;
using Microsoft.EntityFrameworkCore;

namespace Data.Context
{
    public static class ContextSeed
    {
        public static async Task Seed(IDatabaseContextFactory contextFactory, CancellationToken token)
        {
            await using var context = contextFactory.CreateContext();
            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await context.Database.MigrateAsync(token);
            });

            if (!await context.AliasLinks.AnyAsync(token))
            {
                var link = new EFAliasLink();

                context.Clients.Add(new EFClient
                {
                    Active = false,
                    Connections = 0,
                    FirstConnection = DateTime.UtcNow,
                    LastConnection = DateTime.UtcNow,
                    Level = EFClient.Permission.Console,
                    Masked = true,
                    NetworkId = 0,
                    AliasLink = link,
                    CurrentAlias = new EFAlias
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
