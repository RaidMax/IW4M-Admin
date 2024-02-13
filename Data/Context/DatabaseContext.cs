﻿using Microsoft.EntityFrameworkCore;
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
using Data.Models.Zombie;

namespace Data.Context
{
    public abstract class DatabaseContext : DbContext
    {
        public DbSet<EFClient> Clients { get; set; }
        public DbSet<EFAlias> Aliases { get; set; }
        public DbSet<EFAliasLink> AliasLinks { get; set; }
        public DbSet<EFPenalty> Penalties { get; set; }
        public DbSet<EFPenaltyIdentifier> PenaltyIdentifiers { get; set; }
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
        public DbSet<EFClientStatistics> ClientStatistics { get; set; }
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
        
        #region Zombie
        
        public DbSet<ZombieMatch> ZombieMatches { get; set; }
        public DbSet<ZombieMatchClientStat> ZombieMatchClientStats { get; set; }
        public DbSet<ZombieRoundClientStat> ZombieRoundClientStats { get; set; }
        public DbSet<ZombieAggregateClientStat> ZombieClientStatAggregates { get; set; }
        public DbSet<ZombieClientStatRecord> ZombieClientStatRecords { get; set; }
        public DbSet<ZombieEventLog> ZombieEvents { get; set; }

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
            modelBuilder.Entity<EFClient>(entity =>
            {
                entity.HasIndex(client => client.NetworkId);
                entity.HasIndex(client => client.LastConnection);
                entity.HasAlternateKey(client => new
                {
                    client.NetworkId,
                    client.GameName
                });
                

               /* entity.HasMany(prop => prop.ZombieMatchClientStats)
                    .WithOne(prop => prop.Client)
                    .HasForeignKey(prop => prop.ClientId);
                
                entity.HasMany(prop => prop.ZombieRoundClientStats)
                    .WithOne(prop => prop.Client)
                    .HasForeignKey(prop => prop.ClientId);
                
                entity.HasMany(prop => prop.ZombieAggregateClientStats)
                    .WithOne(prop => prop.Client)
                    .HasForeignKey(prop => prop.ClientId);*/
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
                ent.HasIndex(_alias => new {_alias.Name, _alias.IPAddress});
                ent.Property(alias => alias.SearchableIPAddress)
                    .HasMaxLength(255)
                    .HasComputedColumnSql(@"((IPAddress & 255) || '.' || ((IPAddress >> 8) & 255)) || '.' || ((IPAddress >> 16) & 255) || '.' || ((IPAddress >> 24) & 255)", stored: true);
                ent.HasIndex(alias => alias.SearchableIPAddress);
            });

            modelBuilder.Entity<EFMeta>(ent =>
            {
                ent.HasIndex(_meta => _meta.Key);
                ent.HasIndex(_meta => _meta.LinkedMetaId);
                ent.HasOne(_meta => _meta.LinkedMeta)
                    .WithMany()
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<EFPenaltyIdentifier>(ent =>
            {
                ent.HasIndex(identifiers => identifiers.NetworkId);
                ent.HasIndex(identifiers => identifiers.IPv4Address);
            });

            modelBuilder.Entity<EFClientConnectionHistory>(ent => ent.HasIndex(history => history.CreatedDateTime));

            modelBuilder.Entity<EFServerSnapshot>(ent => ent.HasIndex(snapshot => snapshot.CapturedAt));

            // force full name for database conversion
            modelBuilder.Entity<EFClient>().ToTable("EFClients");
            modelBuilder.Entity<EFAlias>().ToTable("EFAlias");
            modelBuilder.Entity<EFAliasLink>().ToTable("EFAliasLinks");
            modelBuilder.Entity<EFPenalty>().ToTable("EFPenalties");
            modelBuilder.Entity<EFPenaltyIdentifier>().ToTable("EFPenaltyIdentifiers");
            modelBuilder.Entity<EFServerSnapshot>().ToTable(nameof(EFServerSnapshot));
            modelBuilder.Entity<EFClientConnectionHistory>().ToTable(nameof(EFClientConnectionHistory));
            
            modelBuilder.Entity<ZombieMatch>().ToTable($"EF{nameof(ZombieMatch)}");
            
            modelBuilder.Entity<ZombieClientStat>(ent =>
            {
                ent.ToTable($"EF{nameof(ZombieClientStat)}");
                ent.HasOne(prop => prop.Client)
                    .WithMany(prop => prop.ZombieClientStats)
                    .HasForeignKey(prop => prop.ClientId);
            });

            modelBuilder.Entity<ZombieMatchClientStat>(ent =>
            {
                ent.ToTable($"EF{nameof(ZombieMatchClientStat)}");
            });

            modelBuilder.Entity<ZombieRoundClientStat>(ent =>
            {
                ent.ToTable($"EF{nameof(ZombieRoundClientStat)}");
            });

            modelBuilder.Entity<ZombieAggregateClientStat>(ent =>
            {
                ent.ToTable($"EF{nameof(ZombieAggregateClientStat)}");
            });

            modelBuilder.Entity<ZombieEventLog>().ToTable($"EF{nameof(ZombieEvents)}");
 
            modelBuilder.Entity<ZombieClientStatRecord>().ToTable($"EF{nameof(ZombieClientStatRecord)}");

            Models.Configuration.StatsModelConfiguration.Configure(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }
    }
}
