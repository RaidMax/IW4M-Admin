using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Data.Models.Server;
using Stats.Models;

namespace Data.Models.Client.Stats
{
    public class EFClientRankingHistory: AuditFields
    {
        [Key]
        public long ClientRankingHistoryId { get; set; }
        
        [Required]
        public int ClientId { get; set; }
        
        [ForeignKey(nameof(ClientId))]
        public virtual EFClient Client { get; set; }
        
        public long? ServerId { get; set; }
        
        [ForeignKey(nameof(ServerId))]
        public virtual EFServer Server { get; set; }
        
        public bool Newest { get; set; }
        public int? Ranking { get; set; }
        public double? ZScore { get; set; }
        public double? PerformanceMetric { get; set; }
    }
}
