using System.ComponentModel.DataAnnotations;
using Data.Abstractions;
using Stats.Models;

namespace Data.Models.Client.Stats.Reference
{
    public class EFWeaponAttachment : AuditFields, IUniqueId
    {
        [Key]
        public int WeaponAttachmentId { get; set; }

        [Required]
        public string Name { get; set; }
        
        [Required]
        public Models.Reference.Game Game { get; set; }

        public long Id => WeaponAttachmentId;
        public string Value => Name;
    }
}
