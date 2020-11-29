using System;
using Microsoft.EntityFrameworkCore;

namespace SharedLibraryCore.Database.MigrationContext
{
    public class SqliteDatabaseContext : DatabaseContext
    {
        public SqliteDatabaseContext()
        {
            if (!Utilities.IsMigration)
            {
                throw new InvalidOperationException();
            }
        }
        
        public SqliteDatabaseContext(DbContextOptions options) : base(options)
        {
            
        }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (Utilities.IsMigration)
            {
                optionsBuilder.UseSqlite("Data Source=IW4MAdmin_Migration.db")
                    .EnableDetailedErrors(true)
                    .EnableSensitiveDataLogging(true);
            }
        }
    }
}