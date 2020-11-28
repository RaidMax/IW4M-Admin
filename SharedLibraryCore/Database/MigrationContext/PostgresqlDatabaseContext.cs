using System;
using Microsoft.EntityFrameworkCore;

namespace SharedLibraryCore.Database.MigrationContext
{
    public class PostgresqlDatabaseContext : DatabaseContext
    {
        public PostgresqlDatabaseContext()
        {
            if (!Utilities.IsMigration)
            {
                throw new InvalidOperationException();
            }
        }
        
        public PostgresqlDatabaseContext(DbContextOptions<PostgresqlDatabaseContext> options) : base(options)
        {
            
        }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (Utilities.IsMigration)
            {
                optionsBuilder.UseNpgsql(
                        "Host=127.0.0.1;Database=IW4MAdmin_Migration;Username=postgres;Password=password;")
                    .EnableDetailedErrors(true)
                    .EnableSensitiveDataLogging(true);
            }
        }
        
    }
}