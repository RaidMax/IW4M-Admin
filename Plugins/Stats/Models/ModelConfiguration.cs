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
        }
    }
}
