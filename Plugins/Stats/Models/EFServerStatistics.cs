using SharedLibraryCore.Database.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IW4MAdmin.Plugins.Stats.Models
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
