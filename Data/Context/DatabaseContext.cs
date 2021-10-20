using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using Data.Extensions;
using Data.Models;
using Data.Models.Client;
using Data.Models.Client.Stats;
using Data.Models.Client.Stats.Reference;
using Data.Models.Misc;
using Data.Models.Server;

namespace Data.Context
{
    public abstract class DatabaseContext : DbContext
    {
        public DbSet<EFClient> Clients { get; set; }
        public DbSet<EFAlias> Aliases { get; set; }
        public DbSet<EFAliasLink> AliasLinks { get; set; }
        public DbSet<EFPenalty> Penalties { get; set; }
        public DbSet<EFMeta> EFMeta { get; set; }
        public DbSet<EFChangeHistory> EFChangeHistory { get; set; }

        #region STATS

        public DbSet<Models.Vector3> Vector3s { get; set; }
        public DbSet<EFACSnapshotVector3> SnapshotVector3s { get; set; }
        public DbSet<EFACSnapshot> ACSnapshots { get; set; }
        public DbSet<EFServer> Servers { get; set; }
        public DbSet<EFClientKill> ClientKills { get; set; }
        public DbSet<EFClientMessage> ClientMessages { get; set; }
        
        public DbSet<EFServerStatistics> ServerStatistics { get; set; }
        public DbSet<EFHitLocation> HitLocations { get; set; }
        public DbSet<EFClientHitStatistic> HitStatistics { get; set; }
        public DbSet<EFWeapon> Weapons { get; set; }
        public DbSet<EFWeaponAttachment> WeaponAttachments { get; set; }
        public DbSet<EFMap> Maps { get; set; }
        
        #endregion

        #region MISC

        public DbSet<EFInboxMessage> InboxMessages { get; set; }
        public DbSet<EFServerSnapshot> ServerSnapshots { get;set; }
        public DbSet<EFClientConnectionHistory> ConnectionHistory { get; set; }

        #endregion

        private void SetAuditColumns()
        {
            return;
        }

        public DatabaseContext()
        {
            if (!MigrationExtensions.IsMigration)
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

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
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
            modelBuilder.Entity<EFClient>(entity => { entity.HasIndex(e => e.NetworkId).IsUnique(); });

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
                ent.HasIndex(_alias => new {_alias.Name, _alias.IPAddress}).IsUnique();
            });

            modelBuilder.Entity<EFMeta>(ent =>
            {
                ent.HasIndex(_meta => _meta.Key);
                ent.HasIndex(_meta => _meta.LinkedMetaId);
                ent.HasOne(_meta => _meta.LinkedMeta)
                    .WithMany()
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<EFClientConnectionHistory>(ent => ent.HasIndex(history => history.CreatedDateTime));

            // force full name for database conversion
            modelBuilder.Entity<EFClient>().ToTable("EFClients");
            modelBuilder.Entity<EFAlias>().ToTable("EFAlias");
            modelBuilder.Entity<EFAliasLink>().ToTable("EFAliasLinks");
            modelBuilder.Entity<EFPenalty>().ToTable("EFPenalties");
            modelBuilder.Entity<EFServerSnapshot>().ToTable(nameof(EFServerSnapshot));
            modelBuilder.Entity<EFClientConnectionHistory>().ToTable(nameof(EFClientConnectionHistory));

            Models.Configuration.StatsModelConfiguration.Configure(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }
    }
}