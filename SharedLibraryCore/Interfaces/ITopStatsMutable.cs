namespace SharedLibraryCore.Interfaces;

// Narrow mutator surface exposed to top-stats transformer hooks (see
// IManager.CustomTopStatsTransformers). Lets premium plugins replace base typed
// fields on top-stats DTOs without SharedLibraryCore taking a dependency on the
// Stats plugin's TopStatsInfo type. The Stats plugin's TopStatsInfo implements
// this interface; transformers receive IList<ITopStatsMutable> and can rewrite
// Kills/Deaths/KDR for buckets where base EFClientStatistics figures don't
// represent the bucket's domain (e.g. zombies bucket, where base bridges only
// zombie kills/damage/deaths to MP-style stats and the resulting weighted KDR
// is meaningless when a player has 0 deaths across most rows).
public interface ITopStatsMutable
{
    int ClientId { get; }
    int Kills { get; set; }
    int Deaths { get; set; }
    double KDR { get; set; }
}
