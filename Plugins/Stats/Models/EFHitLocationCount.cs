using SharedLibraryCore.Database.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IW4MAdmin.Plugins.Stats.Models
{
    public class EFHitLocationCount : SharedEntity
    {
        [Key]
        public int HitLocationCountId { get; set; }
        [Required]
        public IW4Info.HitLocation Location { get; set; }
        [Required]
        public int HitCount { get; set; }
        [Required]
        public float HitOffsetAverage { get; set; }
        [Required]
        public float MaxAngleDistance { get; set; }
        [Required]
        [Column("EFClientStatisticsClientId")]
        public int EFClientStatisticsClientId { get; set; }
        [ForeignKey("EFClientStatisticsClientId")]
        public EFClient Client { get; set; }
        [Column("EFClientStatisticsServerId")]
        public long EFClientStatisticsServerId { get; set; }
        [ForeignKey("EFClientStatisticsServerId")]
        public EFServer Server { get; set; }
    }
}
