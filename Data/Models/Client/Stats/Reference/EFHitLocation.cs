using System.ComponentModel.DataAnnotations;
using Data.Abstractions;
using Stats.Models;

namespace Data.Models.Client.Stats.Reference
{
    public class EFHitLocation : AuditFields, IUniqueId
    {
        [Key]
        public int HitLocationId { get; set; }

        [Required]
        public string Name { get; set; }
        
        [Required]
        public Models.Reference.Game Game { get; set; }

        public long Id => HitLocationId;
        public string Value => Name;
    }
}