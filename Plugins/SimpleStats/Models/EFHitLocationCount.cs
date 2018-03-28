using SharedLibrary.Database.Models;
using System.ComponentModel.DataAnnotations;

namespace StatsPlugin.Models
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
    }
}
