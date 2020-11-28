using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SharedLibraryCore.Database
{
    public abstract class DatabaseContext : DbContext
    {
        public DbSet<EFClient> Clients { get; set; }
        public DbSet<EFAlias> Aliases { get; set; }
        public DbSet<EFAliasLink> AliasLinks { get; set; }
        public DbSet<EFPenalty> Penalties { get; set; }
        public DbSet<EFMeta> EFMeta { get; set; }
        public DbSet<EFChangeHistory> EFChangeHistory { get; set; }

        private void SetAuditColumns()
        {
            return;
            var entries = ChangeTracker
                .Entries()
                .Where(e => e.Entity is SharedEntity && (
                        e.State == EntityState.Added
                        || e.State == EntityState.Modified)).ToList();

            foreach (var entityEntry in entries)
            {
                if (entityEntry.State == EntityState.Added)
                {
                    //((SharedEntity)entityEntry.Entity).CreatedDateTime = DateTime.UtcNow;
                }

                else
                {
                    //((SharedEntity)entityEntry.Entity).UpdatedDateTime = DateTime.UtcNow;
                }
            }
        }

        public DatabaseContext()
        {
            if (!Utilities.IsMigration)
            {
                throw new InvalidOperationException();
            }
        }

        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
        {
            
        }

        protected DatabaseContext(DbContextOptions options) : base(options)
        {
            
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            SetAuditColumns();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public override int SaveChanges()
        {
            SetAuditColumns();
            return base.SaveChanges();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // make network id unique
            modelBuilder.Entity<EFClient>(entity =>
            {
                entity.HasIndex(e => e.NetworkId).IsUnique();
            });

            modelBuilder.Entity<EFPenalty>(entity =>
            {
                entity.HasOne(p => p.Offender)
                .WithMany(c => c.ReceivedPenalties)
                .HasForeignKey(c => c.OffenderId)
                .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Punisher)
                .WithMany(p => p.AdministeredPenalties)
                .HasForeignKey(c => c.PunisherId)
                .OnDelete(DeleteBehavior.Restrict);

                entity.Property(p => p.Expires)
                    .IsRequired(false);
            });

            modelBuilder.Entity<EFAliasLink>(entity =>
            {
                entity.HasMany(e => e.Children)
                .WithOne(a => a.Link)
                .HasForeignKey(k => k.LinkId)
                .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<EFAlias>(ent =>
            {
                ent.Property(a => a.IPAddress).IsRequired(false);
                ent.HasIndex(a => a.IPAddress);
                ent.Property(a => a.Name).HasMaxLength(24);
                ent.HasIndex(a => a.Name);
                ent.Property(_alias => _alias.SearchableName).HasMaxLength(24);
                ent.HasIndex(_alias => _alias.SearchableName);
                ent.HasIndex(_alias => new { _alias.Name, _alias.IPAddress }).IsUnique();
            });

            modelBuilder.Entity<EFMeta>(ent =>
            {
                ent.HasIndex(_meta => _meta.Key);
            });

            // force full name for database conversion
            modelBuilder.Entity<EFClient>().ToTable("EFClients");
            modelBuilder.Entity<EFAlias>().ToTable("EFAlias");
            modelBuilder.Entity<EFAliasLink>().ToTable("EFAliasLinks");
            modelBuilder.Entity<EFPenalty>().ToTable("EFPenalties");

            // adapted from
            // https://aleemkhan.wordpress.com/2013/02/28/dynamically-adding-dbset-properties-in-dbcontext-for-entity-framework-code-first/

            string pluginDir = Path.Join(Utilities.OperatingDirectory, "Plugins");

            if (Utilities.IsDevelopment)
            {
                pluginDir = Path.Join(Utilities.OperatingDirectory, "..", "..", "..", "..", "BUILD", "Plugins");
            }

            IEnumerable<string> directoryFiles = Enumerable.Empty<string>();

            try
            {
                directoryFiles = Directory.GetFiles(pluginDir).Where(f => f.EndsWith(".dll"));
            }

            catch (DirectoryNotFoundException)
            {
                // this is just an ugly thing for unit testing
                directoryFiles = Directory.GetFiles(@"X:\IW4MAdmin\Tests\ApplicationTests\bin\Debug\netcoreapp3.1").Where(f => f.EndsWith("dll"));
            }

            foreach (string dllPath in directoryFiles)
            {
                Assembly library;
                try
                {
                    library = Assembly.LoadFrom(dllPath);
                }

                // not a valid assembly, ie plugin support files
                catch (Exception)
                {
                    continue;
                }

                var configurations = library.ExportedTypes.Where(c => c.GetInterfaces().FirstOrDefault(i => typeof(IModelConfiguration).IsAssignableFrom(i)) != null)
                    .Select(c => (IModelConfiguration)Activator.CreateInstance(c));

                foreach (var configurable in configurations)
                {
                    configurable.Configure(modelBuilder);
                }

                foreach (var type in library.ExportedTypes)
                {
                    if (type.IsClass && type.IsSubclassOf(typeof(SharedEntity)))
                    {
                        var method = modelBuilder.GetType().GetMethod("Entity", new[] { typeof(Type) });
                        method.Invoke(modelBuilder, new[] { type });
                    }
                }
            }

            base.OnModelCreating(modelBuilder);
        }
    }
}
