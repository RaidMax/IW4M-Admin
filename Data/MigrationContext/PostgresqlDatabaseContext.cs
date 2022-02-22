using System;
using Data.Context;
using Data.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Data.MigrationContext
{
    public class PostgresqlDatabaseContext : DatabaseContext
    {
        public PostgresqlDatabaseContext()
        {
            if (!MigrationExtensions.IsMigration)
            {
                throw new InvalidOperationException();
            }
        }

        public PostgresqlDatabaseContext(DbContextOptions options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (MigrationExtensions.IsMigration)
            {
                AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                optionsBuilder.UseNpgsql(
                        "Host=127.0.0.1;Database=IW4MAdmin_Migration;Username=postgres;Password=password;",
                        options => options.SetPostgresVersion(new Version("12.9")))
                    .EnableDetailedErrors(true)
                    .EnableSensitiveDataLogging(true);
            }
        }
    }
}
