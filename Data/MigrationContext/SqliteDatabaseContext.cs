using System;
using Data.Context;
using Data.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Data.MigrationContext
{
    public class SqliteDatabaseContext : DatabaseContext
    {
        public SqliteDatabaseContext()
        {
            if (!MigrationExtensions.IsMigration)
            {
                throw new InvalidOperationException();
            }
        }
        
        public SqliteDatabaseContext(DbContextOptions options) : base(options)
        {
            
        }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (MigrationExtensions.IsMigration)
            {
                optionsBuilder.UseSqlite("Data Source=IW4MAdmin_Migration.db")
                    .EnableDetailedErrors(true)
                    .EnableSensitiveDataLogging(true);
            }
        }
    }
}