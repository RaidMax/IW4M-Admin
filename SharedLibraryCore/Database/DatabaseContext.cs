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


        //[Obsolete]
        //private static readonly ILoggerFactory _loggerFactory = new LoggerFactory(new[] {
        //      new ConsoleLoggerProvider((category, level) => level == LogLevel.Information, true)
        //});

        static string _ConnectionString;
        static string _provider;
        private static readonly string _migrationPluginDirectory = @"X:\IW4MAdmin\BUILD\Plugins";
        private static int activeContextCount;

        public DatabaseContext(DbContextOptions<DatabaseContext> opt) : base(opt)
        {
#if DEBUG == true
            activeContextCount++;
            //Console.WriteLine($"Initialized DB Context #{activeContextCount}");
#endif
        }

        public DatabaseContext()
        {
#if DEBUG == true
            activeContextCount++;
            //Console.WriteLine($"Initialized DB Context #{activeContextCount}");
#endif
        }

        public override void Dispose()
        {
#if DEBUG == true

            //Console.WriteLine($"Disposed DB Context #{activeContextCount}");
            activeContextCount--;
#endif
        }

        public DatabaseContext(bool disableTracking) : this()
        {
            if (disableTracking)
            {
                this.ChangeTracker.AutoDetectChangesEnabled = false;
                this.ChangeTracker.LazyLoadingEnabled = false;
                this.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            }

            else
            {
                this.ChangeTracker.AutoDetectChangesEnabled = true;
                this.ChangeTracker.LazyLoadingEnabled = true;
                this.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
            }
        }

        public DatabaseContext(string connStr, string provider) : this()
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

                var connectionStringBuilder = new SqliteConnectionStringBuilder { DataSource = Path.Join(currentPath, "Database", "Database.db") };
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
#if DEBUG
            string pluginDir = _migrationPluginDirectory;
#else
            string pluginDir = Path.Join(Utilities.OperatingDirectory, "Plugins");
#endif
            IEnumerable<string> directoryFiles = Directory.GetFiles(pluginDir).Where(f => f.EndsWith(".dll"));

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
