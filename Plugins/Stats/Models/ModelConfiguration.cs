using Microsoft.EntityFrameworkCore;

using SharedLibraryCore.Interfaces;
using IW4MAdmin.Plugins.Stats.Models;

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
                .Property(c => c.ClientId)
                .HasColumnName("EFClientStatistics_ClientId");

            builder.Entity<EFHitLocationCount>()
                .Property(c => c.ServerId)
                .HasColumnName("EFClientStatistics_ServerId");

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
