using Data.Models.Client;
using Data.Models.Client.Stats;
using Data.Models.Client.Stats.Reference;
using Data.Models.Server;
using Microsoft.EntityFrameworkCore;

namespace Data.Models.Configuration
{
    public class StatsModelConfiguration
    {
        public static void Configure(ModelBuilder builder)
        {
            builder.Entity<EFClientStatistics>(entity =>
            {
                entity.HasKey(cs => new {cs.ClientId, cs.ServerId});
                entity.HasIndex(cs => new {cs.ClientId, cs.TimePlayed, PerformancePercentile = cs.ZScore});
                entity.HasIndex(cs => new {PerformancePercentile = cs.ZScore});
                entity.ToTable("EFClientStatistics");
            });


            // fix linking from SQLCe
            builder.Entity<EFHitLocationCount>(entity =>
            {
                entity.Property(c => c.EFClientStatisticsClientId)
                    .HasColumnName("EFClientStatisticsClientId");
                entity.Property(c => c.EFClientStatisticsServerId)
                    .HasColumnName("EFClientStatisticsServerId");

                entity.ToTable("EFHitLocationCounts");
            });


            builder.Entity<EFRating>(entity =>
            {
                entity.HasIndex(p => new {p.Performance, p.Ranking, p.When});
                entity.HasIndex(p => new {p.When, p.ServerId, p.Performance, p.ActivityAmount});
                entity.ToTable(nameof(EFRating));
            });


            builder.Entity<EFClientMessage>(entity =>
            {
                entity.HasIndex(p => p.TimeSent);
                entity.ToTable("EFClientMessages");
            });

            builder.Entity<EFClientStatistics>(entity => { entity.ToTable(nameof(EFClientStatistics)); });

            builder.Entity<EFRating>(entity => { entity.ToTable(nameof(EFRating)); });

            builder.Entity<EFClientRatingHistory>(entity => { entity.ToTable(nameof(EFClientRatingHistory)); });

            builder.Entity<EFHitLocationCount>(entity => { entity.ToTable("EFHitLocationCounts"); });

            builder.Entity<EFServerStatistics>(entity => { entity.ToTable("EFServerStatistics"); });

            builder.Entity<EFServer>(entity => { entity.ToTable("EFServers"); });

            builder.Entity<EFClientKill>(entity => { entity.ToTable("EFClientKills"); });

            builder.Entity<Vector3>().ToTable(nameof(Vector3));
            builder.Entity<EFACSnapshot>().ToTable(nameof(EFACSnapshot));
            builder.Entity<EFACSnapshotVector3>().ToTable(nameof(EFACSnapshotVector3));

            builder.Entity<EFHitLocation>(entity =>
            {
                entity.HasIndex(loc => loc.Name);
                entity.ToTable("EFHitLocations");
            });

            builder.Entity<EFWeapon>(entity =>
            {
                entity.HasIndex(weapon => weapon.Name);
                entity.ToTable("EFWeapons");
            });

            builder.Entity<EFMap>(entity => { entity.ToTable("EFMaps"); });
            builder.Entity<EFClientHitStatistic>(entity => { entity.ToTable("EFClientHitStatistics"); });
            builder.Entity<EFWeaponAttachment>(entity => { entity.ToTable("EFWeaponAttachments"); });
            builder.Entity<EFWeaponAttachmentCombo>(entity => { entity.ToTable("EFWeaponAttachmentCombos"); });
            builder.Entity<EFMeansOfDeath>(entity => { entity.ToTable("EFMeansOfDeath"); });
            builder.Entity<EFClientRankingHistory>(entity =>
            {
                entity.ToTable(nameof(EFClientRankingHistory));
                entity.HasIndex(ranking => ranking.Ranking);
                entity.HasIndex(ranking => ranking.ZScore);
                entity.HasIndex(ranking => ranking.UpdatedDateTime);
                entity.HasIndex(ranking => ranking.CreatedDateTime);
            });
        }
    }
}
