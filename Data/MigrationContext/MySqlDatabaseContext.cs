using System;
using Data.Context;
using Data.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Data.MigrationContext
{
    public class MySqlDatabaseContext : DatabaseContext
    {
        public MySqlDatabaseContext()
        {
            if (!MigrationExtensions.IsMigration)
            {
                throw new InvalidOperationException();
            }
        }

        public MySqlDatabaseContext(DbContextOptions options) : base(options)
        {
            
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (MigrationExtensions.IsMigration)
            {
                // Note: ServerVersion.AutoDetect requires a live MySQL server connection.
                // To generate migrations without a running server, temporarily replace with:
                // new MySqlServerVersion(new Version(8, 0, 35)) then revert after generation.
                var connectionString = "Server=127.0.0.1;Database=IW4MAdmin_Migration;Uid=root;Pwd=password;";
                optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                    .EnableDetailedErrors()
                    .EnableSensitiveDataLogging();
            }
        }
    }
}
