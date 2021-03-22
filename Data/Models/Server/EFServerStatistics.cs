using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Models.Server
{
    public class EFServerStatistics : SharedEntity
    {
        [Key]
        public int StatisticId { get; set; }
        public long ServerId { get; set; }
        [ForeignKey("ServerId")]
        public virtual EFServer Server { get; set; }
        public long TotalKills { get; set; }
        public long TotalPlayTime { get; set; }
    }
}
