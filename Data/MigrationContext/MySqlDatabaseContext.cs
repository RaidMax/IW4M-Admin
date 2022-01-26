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
                optionsBuilder.UseMySql(ServerVersion.AutoDetect("Server=127.0.0.1;Database=IW4MAdmin_Migration;Uid=root;Pwd=password;"))
                    .EnableDetailedErrors()
                    .EnableSensitiveDataLogging();
            }
        }
    }
}
