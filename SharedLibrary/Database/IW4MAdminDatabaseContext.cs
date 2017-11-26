using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharedLibrary.Database.Models;
using System.Data.SqlServerCe;

namespace SharedLibrary.Database
{
    public class IW4MAdminDatabaseContext : DbContext
    {
        public DbSet<EFClient> Clients { get; set; }
        public DbSet<EFAlias> Aliases { get; set; }
        public DbSet<EFAliasLink> AliasLinks { get; set; }
        public DbSet<EFPenalty> Penalties { get; set; }

        public IW4MAdminDatabaseContext() : base("DefaultConnection")
        {
            System.Data.Entity.Database.SetInitializer(new Initializer());
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EFPenalty>()
                      .HasRequired(p => p.Punisher)
                      .WithMany(c => c.AdministeredPenalties)
                      .HasForeignKey(c => c.PunisherId)
                      .WillCascadeOnDelete(false);

            modelBuilder.Entity<EFPenalty>()
                     .HasRequired(p => p.Offender)
                     .WithMany(c => c.ReceivedPenalties)
                     .HasForeignKey(c => c.OffenderId)
                     .WillCascadeOnDelete(false);

            modelBuilder.Entity<EFAliasLink>()
                .HasMany(e => e.Children)
                .WithRequired(a => a.Link)
                .HasForeignKey(a => a.LinkId)
                .WillCascadeOnDelete(true);

            // todo custom load DBSets from plugins
            // https://aleemkhan.wordpress.com/2013/02/28/dynamically-adding-dbset-properties-in-dbcontext-for-entity-framework-code-first/
            base.OnModelCreating(modelBuilder);
        }
    }
}
