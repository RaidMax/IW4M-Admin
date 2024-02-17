using System.Collections.Generic;
using Data.Models;
using Data.Models.Client;
using Data.Models.Client.Stats;
using SharedLibraryCore.Dtos;

namespace Stats.Dtos
{
    public class AdvancedStatsInfo
    {
        public long? ServerId { get; set; }
        public string ServerEndpoint { get; set; }
        public string ClientName { get; set; }
        public int ClientId { get; set; }
        public EFClient.Permission Level { get; set; }
        public double? Performance { get; set; }
        public int? Ranking { get; set; }
        public int TotalRankedClients { get; set; }
        public double? ZScore { get; set; }
        public double? Rating { get; set; }
        public List<ServerInfo> Servers { get; set; }
        public List<EFClientHitStatistic> All { get; set; }
        public EFClientHitStatistic Aggregate { get; set; }
        public List<EFClientHitStatistic> ByHitLocation { get; set; }
        public List<EFClientHitStatistic> ByWeapon { get; set; }
        public List<EFClientHitStatistic> ByAttachmentCombo { get; set; }
        public List<EFClientRankingHistory> Ratings { get; set; }
        public List<EFClientStatistics> LegacyStats { get; set; }
        public List<EFMeta> CustomMetrics { get; set; } = new();
        public string PerformanceBucket { get; set; }
    }
}
