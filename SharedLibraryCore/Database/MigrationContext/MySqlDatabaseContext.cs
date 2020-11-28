using System;
using Microsoft.EntityFrameworkCore;

namespace SharedLibraryCore.Database.MigrationContext
{
    public class MySqlDatabaseContext : DatabaseContext
    {
        public MySqlDatabaseContext()
        {
            if (!Utilities.IsMigration)
            {
                throw new InvalidOperationException();
            }
        }

        public MySqlDatabaseContext(DbContextOptions<MySqlDatabaseContext> options) : base(options)
        {
            
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (Utilities.IsMigration)
            {
                optionsBuilder.UseMySql("Server=127.0.0.1;Database=IW4MAdmin_Migration;Uid=root;Pwd=password;")
                    .EnableDetailedErrors(true)
                    .EnableSensitiveDataLogging(true);
            }
        }
    }
}