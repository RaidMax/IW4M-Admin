using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Database.Models;
using System.Data.SqlServerCe;

namespace Database
{
    public class IW4MAdminDatabaseContext : DbContext, SharedLibrary.Interfaces.IDatabaseContext
    {
        public DbSet<Client> Clients { get; set; }
        public DbSet<Alias> Aliases { get; set; }
        public DbSet<Penalty> Penalties { get; set; }

        public IW4MAdminDatabaseContext() : base("DefaultConnection") { }
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            var instance = System.Data.Entity.SqlServerCompact.SqlCeProviderServices.Instance;
            // todo custom load DBSets from plugins
            // https://aleemkhan.wordpress.com/2013/02/28/dynamically-adding-dbset-properties-in-dbcontext-for-entity-framework-code-first/
            base.OnModelCreating(modelBuilder);
        }
    }
}
