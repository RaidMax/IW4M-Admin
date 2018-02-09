using SharedLibrary.Database.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StatsPlugin.Models
{
    public class EFServerStatistics : SharedEntity
    {
        [Key]
        public int StatisticId { get; set; }
        public int ServerId { get; set; }
        [ForeignKey("ServerId")]
        public virtual EFServer Server { get; set; }
        public long TotalKills { get; set; }
        public long TotalPlayTime { get; set; }
    }
}
