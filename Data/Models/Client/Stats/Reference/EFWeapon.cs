using System.ComponentModel.DataAnnotations;
using Data.Abstractions;
using Stats.Models;

namespace Data.Models.Client.Stats.Reference
{
    public class EFWeapon : AuditFields, IUniqueId
    {
        [Key]
        public int WeaponId { get; set; }
        
        [Required]
        public string Name { get; set; }
        
        [Required]
        public Models.Reference.Game Game { get; set; }

        public long Id => WeaponId;
        public string Value => Name;
    }
}