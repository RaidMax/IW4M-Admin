using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharedLibrary.Database.Models;
using System.Data.SqlServerCe;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Reflection;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.SqlServerCompact;

namespace SharedLibrary.Database
{

    public class DatabaseContext : DbContext
    {
        public DbSet<EFClient> Clients { get; set; }
        public DbSet<EFAlias> Aliases { get; set; }
        public DbSet<EFAliasLink> AliasLinks { get; set; }
        public DbSet<EFPenalty> Penalties { get; set; }

        public DatabaseContext() : base("DefaultConnection")
        {
            System.Data.Entity.Database.SetInitializer(new Initializer());
            Configuration.LazyLoadingEnabled = true;
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EFPenalty>()
                .HasRequired(p => p.Offender)
                .WithMany(c => c.ReceivedPenalties)
                .HasForeignKey(c => c.OffenderId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<EFPenalty>()
                .HasRequired(p => p.Punisher)
                .WithMany(c => c.AdministeredPenalties)
                .HasForeignKey(c => c.PunisherId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<EFAliasLink>()
                .HasMany(e => e.Children)
                .WithRequired(a => a.Link)
                .HasForeignKey(a => a.LinkId)
                .WillCascadeOnDelete(true);

            modelBuilder.Conventions.Remove<OneToManyCascadeDeleteConvention>();

            // https://aleemkhan.wordpress.com/2013/02/28/dynamically-adding-dbset-properties-in-dbcontext-for-entity-framework-code-first/
            foreach (string dllPath in System.IO.Directory.GetFiles($"{Utilities.OperatingDirectory}Plugins"))
            {
                Assembly library;
                try
                {
                    library = Assembly.LoadFile(dllPath);
                }

                // not a valid assembly, ie plugin files
                catch (Exception)
                {
                    continue;
                }

                foreach(var type in library.ExportedTypes)
                {
                    if (type.IsClass && type.IsSubclassOf(typeof(SharedEntity)))
                    {
                        var method = modelBuilder.GetType().GetMethod("Entity");
                        method = method.MakeGenericMethod(new Type[] { type });
                        method.Invoke(modelBuilder, null);
                    }
                }
            }

            base.OnModelCreating(modelBuilder);
        }
    }
}
