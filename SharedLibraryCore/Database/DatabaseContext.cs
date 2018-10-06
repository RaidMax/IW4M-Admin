using System;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database.Models;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using SharedLibraryCore.Interfaces;
using System.Runtime.InteropServices;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace SharedLibraryCore.Database
{
    public class DatabaseContext : DbContext
    {
        public DbSet<EFClient> Clients { get; set; }
        public DbSet<EFAlias> Aliases { get; set; }
        public DbSet<EFAliasLink> AliasLinks { get; set; }
        public DbSet<EFPenalty> Penalties { get; set; }
        public DbSet<EFMeta> EFMeta { get; set; }
        public DbSet<EFChangeHistory> EFChangeHistory { get; set; }

        static string _ConnectionString;
        static string _provider;

        public DatabaseContext(DbContextOptions<DatabaseContext> opt) : base(opt) { }

        public DatabaseContext() { }

        public DatabaseContext(bool disableTracking)
        {
            if (disableTracking)
            {
                this.ChangeTracker.AutoDetectChangesEnabled = false;
                this.ChangeTracker.LazyLoadingEnabled = false;
                this.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            }
        }

        public DatabaseContext(string connStr, string provider)
        {
            _ConnectionString = connStr;
            _provider = provider;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (string.IsNullOrEmpty(_ConnectionString))
            {
                string currentPath = Utilities.OperatingDirectory;
                // allows the application to find the database file
                currentPath = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                    $"{Path.DirectorySeparatorChar}{currentPath}" :
                    currentPath;

                var connectionStringBuilder = new SqliteConnectionStringBuilder { DataSource = $"{currentPath}{Path.DirectorySeparatorChar}Database{Path.DirectorySeparatorChar}Database.db" };
                var connectionString = connectionStringBuilder.ToString();
                var connection = new SqliteConnection(connectionString);

                optionsBuilder.UseSqlite(connection);
            }

            else
            {
                switch (_provider)
                {
                    default:
                    case "mysql":
                        optionsBuilder.UseMySql(_ConnectionString);
                        break;
                    case "postgresql":
                        optionsBuilder.UseNpgsql(_ConnectionString);
                        break;
                }
            }
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

            modelBuilder.Entity<EFAlias>(ent =>
            {
                ent.HasIndex(a => a.IPAddress);
                ent.Property(a => a.Name).HasMaxLength(24);
                ent.HasIndex(a => a.Name);
            });

            // force full name for database conversion
            modelBuilder.Entity<EFClient>().ToTable("EFClients");
            modelBuilder.Entity<EFAlias>().ToTable("EFAlias");
            modelBuilder.Entity<EFAliasLink>().ToTable("EFAliasLinks");
            modelBuilder.Entity<EFPenalty>().ToTable("EFPenalties");

            // adapted from
            // https://aleemkhan.wordpress.com/2013/02/28/dynamically-adding-dbset-properties-in-dbcontext-for-entity-framework-code-first/
            IEnumerable<string> directoryFiles;

            string pluginDir = $@"{Environment.CurrentDirectory}{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}Debug{Path.DirectorySeparatorChar}netcoreapp2.0{Path.DirectorySeparatorChar}Plugins";

            if (!Directory.Exists(pluginDir))
            {
                pluginDir = $@"{Environment.CurrentDirectory}{Path.DirectorySeparatorChar}Plugins";

                if (!Directory.Exists(pluginDir))
                {
                    pluginDir = Utilities.OperatingDirectory;
                }
            }

            directoryFiles = Directory.GetFiles(pluginDir).Where(f => f.EndsWith(".dll"));
#if DEBUG == TRUE
            foreach (string dllPath in Directory.GetFiles(@"C:\Projects\IW4M-Admin\Application\bin\Debug\netcoreapp2.1\Plugins").Where(f => f.EndsWith(".dll")))
#else
            foreach (string dllPath in directoryFiles)
#endif
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
