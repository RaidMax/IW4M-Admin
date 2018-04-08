using System;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database.Models;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Database
{
    public class DatabaseContext : DbContext
    {
        public DbSet<EFClient> Clients { get; set; }
        public DbSet<EFAlias> Aliases { get; set; }
        public DbSet<EFAliasLink> AliasLinks { get; set; }
        public DbSet<EFPenalty> Penalties { get; set; }

        public DatabaseContext(DbContextOptions<DatabaseContext> opt) : base(opt) { }

        public DatabaseContext()
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);
            var connectionStringBuilder = new SqliteConnectionStringBuilder { DataSource = $"{currentPath}{Path.DirectorySeparatorChar}Database.db".Substring(6) };
            var connectionString = connectionStringBuilder.ToString();
            var connection = new SqliteConnection(connectionString);

            optionsBuilder.UseSqlite(connection);
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
            });

            modelBuilder.Entity<EFAliasLink>(entity =>
            {
                entity.HasMany(e => e.Children)
                .WithOne(a => a.Link)
                .HasForeignKey(k => k.LinkId)
                .OnDelete(DeleteBehavior.Restrict);
            });

            // https://aleemkhan.wordpress.com/2013/02/28/dynamically-adding-dbset-properties-in-dbcontext-for-entity-framework-code-first/
#if !DEBUG
            foreach (string dllPath in Directory.GetFiles($"{Utilities.OperatingDirectory}Plugins"))
#else
            IEnumerable<string> directoryFiles;
            try
            {
                directoryFiles = Directory.GetFiles($@"{Environment.CurrentDirectory}{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}Debug{Path.DirectorySeparatorChar}netcoreapp2.0{Path.DirectorySeparatorChar}Plugins").Where(f => f.Contains(".dll"));
            }

            catch (Exception)
            {
                directoryFiles = Directory.GetFiles($@"{Environment.CurrentDirectory}{Path.DirectorySeparatorChar}Plugins").Where(f => f.Contains(".dll"));
            }

            foreach (string dllPath in directoryFiles)
#endif
            {
                Assembly library;
                try
                {
                    library = Assembly.LoadFrom(dllPath);
                }

                // not a valid assembly, ie plugin files
                catch (Exception)
                {
                    continue;
                }
                
                var configurations = library.ExportedTypes.Where(c => c.GetInterfaces().FirstOrDefault(i => typeof(IModelConfiguration).IsAssignableFrom(i)) != null)
                    .Select( c => (IModelConfiguration)Activator.CreateInstance(c));

                foreach (var configurable in configurations)
                    configurable.Configure(modelBuilder);

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
