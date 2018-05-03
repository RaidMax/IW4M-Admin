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
        public int ClientId { get; set; }
        [ForeignKey("ClientId"), Column(Order = 0 )]
        public EFClient Client { get; set; }
        public int ServerId { get; set; }
        [ForeignKey("ServerId"), Column(Order = 1)]
        public EFServer Server { get; set; }

    }
}
