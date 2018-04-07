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
using System.IO;
using System.Data.Common;

namespace SharedLibrary.Database
{

    [DbConfigurationType(typeof(ContextConfiguration))]
    public class DatabaseContext : DbContext
    {
        public DbSet<EFClient> Clients { get; set; }
        public DbSet<EFAlias> Aliases { get; set; }
        public DbSet<EFAliasLink> AliasLinks { get; set; }
        public DbSet<EFPenalty> Penalties { get; set; }

        public static string ConnectionString;

        public DatabaseContext() : base(ConnectionString)
        {
            System.Data.Entity.Database.SetInitializer(new MigrateDatabaseToLatestVersion<DatabaseContext, Migrations.Configuration>());
            //Database.CreateIfNotExists();
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
            //string dir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar; 
#if !DEBUG
            foreach (string dllPath in System.IO.Directory.GetFiles($"{Utilities.OperatingDirectory}Plugins"))
#else
            IEnumerable<string> directoryFiles;
            try
            {
                directoryFiles = Directory.GetFiles($@"{Environment.CurrentDirectory}\bin\x86\Debug\Plugins").Where(f => f.Contains(".dll"));
            }

            catch (Exception)
            {
                directoryFiles = Directory.GetFiles($@"{Environment.CurrentDirectory}\Plugins").Where(f => f.Contains(".dll"));
            }

            foreach (string dllPath in directoryFiles)
#endif
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

                foreach (var type in library.ExportedTypes)
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
