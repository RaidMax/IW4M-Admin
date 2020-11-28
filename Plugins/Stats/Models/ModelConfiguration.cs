using IW4MAdmin.Plugins.Stats.Models;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Interfaces;

namespace Stats.Models
{
    public class ModelConfiguration : IModelConfiguration
    {
        public void Configure(ModelBuilder builder)
        {
            builder.Entity<EFClientStatistics>()
                .HasKey(cs => new { cs.ClientId, cs.ServerId });

            // fix linking from SQLCe
            builder.Entity<EFHitLocationCount>()
                .Property(c => c.EFClientStatisticsClientId)
                .HasColumnName("EFClientStatisticsClientId");

            builder.Entity<EFHitLocationCount>()
                .Property(c => c.EFClientStatisticsServerId)
                .HasColumnName("EFClientStatisticsServerId");

            builder.Entity<EFRating>()
                .HasIndex(p => new { p.Performance, p.Ranking, p.When });
            
            builder.Entity<EFRating>()
                .HasIndex(p => new { p.When, p.ServerId, p.Performance, p.ActivityAmount });

            builder.Entity<EFClientMessage>(message =>
            {
                message.HasIndex(p => p.TimeSent);
            });
 
            // force pluralization
            builder.Entity<EFClientKill>().ToTable("EFClientKills");
            builder.Entity<EFClientMessage>().ToTable("EFClientMessages");
            builder.Entity<EFClientStatistics>().ToTable("EFClientStatistics");
            builder.Entity<EFHitLocationCount>().ToTable("EFHitLocationCounts");
            builder.Entity<EFServer>().ToTable("EFServers");
            builder.Entity<EFServerStatistics>().ToTable("EFServerStatistics");
        }
    }
}
