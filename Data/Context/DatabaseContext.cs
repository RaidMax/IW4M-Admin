using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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

        public DbSet<EFPerformanceBucket> PerformanceBuckets { get; set; }
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
        
        public DbSet<EFClientStatTag> ClientStatTags { get; set; }
        public DbSet<EFClientStatTagValue> ClientStatTagValues { get; set; }
        public DbSet<EFMap> Maps { get; set; }
        
        #endregion

        #region MISC

        public DbSet<EFInboxMessage> InboxMessages { get; set; }
        public DbSet<EFAnnouncement> Announcements { get; set; }
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
        public DbSet<ZombieRoundDurationEma> ZombieRoundDurationEmas { get; set; }
        
        #endregion

        /// <summary>
        /// Shadow-column names backing the zombie aggregate dedupe key. Shadow (not CLR)
        /// properties so callers can't drift them out of sync with ClientId/ServerId —
        /// they're stamped on every save instead.
        /// </summary>
        public const string ZombieAggregateDedupeClientIdColumn = "DedupeClientId";
        public const string ZombieAggregateDedupeServerIdColumn = "DedupeServerId";
        public const string ZombieAggregateDedupeIndexName = "IX_EFZombieClientStatAggregates_DedupeKey";

        private void SetAuditColumns()
        {
            return;
        }

        private void StampZombieAggregateDedupeKeys()
        {
            foreach (var entry in ChangeTracker.Entries<ZombieAggregateClientStat>())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified))
                {
                    continue;
                }

                entry.Property(ZombieAggregateDedupeClientIdColumn).CurrentValue = entry.Entity.ClientId;
                entry.Property(ZombieAggregateDedupeServerIdColumn).CurrentValue = entry.Entity.ServerId ?? -1L;
            }
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
            StampZombieAggregateDedupeKeys();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public override int SaveChanges()
        {
            SetAuditColumns();
            StampZombieAggregateDedupeKeys();
            return base.SaveChanges();
        }

        /// <summary>
        /// SQLite has no native <see cref="DateTimeOffset"/> type and the EF Core
        /// SQLite provider can't translate <c>ORDER BY</c> on DateTimeOffset columns
        /// to SQL (throws <c>NotSupportedException</c> at query compile time). The
        /// canonical fix is a value converter that stores DateTimeOffset as a long
        /// (binary tick representation) — comparable, ORDER BY-able, and round-trips
        /// the offset losslessly via <see cref="DateTimeOffset.ToBinary"/>.
        ///
        /// Postgres + MySQL handle DateTimeOffset natively, so the converter is only
        /// applied when running on SQLite (detected via <see cref="DatabaseFacade.ProviderName"/>).
        /// Apply via <c>ConfigureConventions</c> so it covers every DateTimeOffset
        /// property in every entity globally — no risk of forgetting one as new
        /// models are added.
        /// </summary>
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);

            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                configurationBuilder.Properties<DateTimeOffset>()
                    .HaveConversion<DateTimeOffsetToBinaryConverter>();
                configurationBuilder.Properties<DateTimeOffset?>()
                    .HaveConversion<DateTimeOffsetToBinaryConverter>();
            }
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

            modelBuilder.Entity<EFAnnouncement>(ent =>
            {
                ent.HasIndex(a => a.IsActive);
                ent.HasIndex(a => a.IsGlobalNotice);
                ent.HasOne(a => a.CreatedByClient)
                    .WithMany()
                    .HasForeignKey(a => a.CreatedByClientId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<EFAnnouncement>().ToTable(nameof(EFAnnouncement));

            // force full name for database conversion
            modelBuilder.Entity<EFClient>().ToTable("EFClients");
            modelBuilder.Entity<EFAlias>().ToTable("EFAlias");
            modelBuilder.Entity<EFAliasLink>().ToTable("EFAliasLinks");
            modelBuilder.Entity<EFPenalty>().ToTable("EFPenalties");
            modelBuilder.Entity<EFPenaltyIdentifier>().ToTable("EFPenaltyIdentifiers");
            modelBuilder.Entity<EFServerSnapshot>().ToTable(nameof(EFServerSnapshot));
            modelBuilder.Entity<EFClientConnectionHistory>().ToTable(nameof(EFClientConnectionHistory));
            
            modelBuilder.Entity<ZombieMatch>(ent =>
            {
                ent.ToTable($"EF{nameof(ZombieMatches)}");
                // Index supports the GSC stitching lookup: TrackClient checks
                // for an open match on this server with the same GameMatchId.
                ent.HasIndex(m => new { m.ServerId, m.GameMatchId, m.MatchEndDate });
            });
            
            modelBuilder.Entity<ZombieClientStat>(ent =>
            {
                ent.ToTable($"EF{nameof(ZombieClientStat)}s");
                ent.HasOne(prop => prop.Client)
                    .WithMany(prop => prop.ZombieClientStats)
                    .HasForeignKey(prop => prop.ClientId);
            });

            modelBuilder.Entity<ZombieMatchClientStat>(ent =>
            {
                ent.ToTable($"EF{nameof(ZombieMatchClientStats)}");
            });

            modelBuilder.Entity<ZombieRoundClientStat>(ent =>
            {
                ent.ToTable($"EF{nameof(ZombieRoundClientStats)}");
            });

            modelBuilder.Entity<ZombieAggregateClientStat>(ent =>
            {
                ent.ToTable($"EF{nameof(ZombieClientStatAggregates)}");
                // One aggregate row per (client, server) — server NULL meaning "lifetime".
                // TPT puts ClientId on the base table and ServerId on this child table, so
                // the natural UNIQUE(ClientId, ServerId) can't be expressed as a single
                // index. Instead both halves are denormalized into shadow columns on this
                // table, stamped automatically in SaveChanges (see
                // StampZombieAggregateDedupeKeys). ServerId NULL maps to -1 because every
                // supported provider treats NULLs as distinct in unique indexes, which
                // would let duplicate lifetime rows through.
                ent.Property<int>(ZombieAggregateDedupeClientIdColumn);
                ent.Property<long>(ZombieAggregateDedupeServerIdColumn).HasDefaultValue(-1L);
                ent.HasIndex(ZombieAggregateDedupeClientIdColumn, ZombieAggregateDedupeServerIdColumn)
                    .IsUnique()
                    .HasDatabaseName(ZombieAggregateDedupeIndexName);
            });

            modelBuilder.Entity<ZombieEventLog>(ent =>
            {
                ent.ToTable($"EF{nameof(ZombieEvents)}");
                // Composite indexes for hot event-log filter paths:
                //   • (MatchId, EventType): per-match event-type filters
                //     (timeline rendering, "all gum events for match X" etc.).
                //   • (SourceClientId, EventType): per-client lifetime
                //     event-type counts (career bank ops, gum activations).
                ent.HasIndex(e => new { e.MatchId, e.EventType });
                ent.HasIndex(e => new { e.SourceClientId, e.EventType });
            });

            modelBuilder.Entity<ZombieClientStatRecord>(ent =>
            {
                ent.ToTable($"EF{nameof(ZombieClientStatRecords)}");
            });

            modelBuilder.Entity<ZombieRoundDurationEma>(ent =>
            {
                ent.ToTable($"EF{nameof(ZombieRoundDurationEmas)}");
                ent.HasKey(e => new { e.MapId, e.RoundNumber, e.PlayerCount });
            });

            modelBuilder.Entity<EFPerformanceBucket>(ent =>
            {
                ent.ToTable($"EF{nameof(PerformanceBuckets)}");
            });

            modelBuilder.Entity<EFClientStatTag>(ent =>
            {
                ent.ToTable($"EF{nameof(ClientStatTags)}");
            });

            modelBuilder.Entity<EFClientStatTagValue>(ent =>
            {
                ent.ToTable($"EF{nameof(ClientStatTagValues)}");
            });
            
            Models.Configuration.StatsModelConfiguration.Configure(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }
    }
}
