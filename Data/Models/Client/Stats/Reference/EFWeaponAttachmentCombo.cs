using Data.Abstractions;
using Stats.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Models.Client.Stats.Reference
{
    public class EFWeaponAttachmentCombo : AuditFields, IUniqueId
    {
        [Key]
        public int WeaponAttachmentComboId { get; set; }
        
        [Required]
        public Models.Reference.Game Game { get; set; }

        [Required]
        public int Attachment1Id { get; set; }

        [ForeignKey(nameof(Attachment1Id))]
        public virtual EFWeaponAttachment Attachment1 { get; set; }

        public int? Attachment2Id { get; set; }

        [ForeignKey(nameof(Attachment2Id))]
        public virtual EFWeaponAttachment Attachment2 { get; set; }

        public int? Attachment3Id { get; set; }

        [ForeignKey(nameof(Attachment3Id))]
        public virtual EFWeaponAttachment Attachment3 { get; set; }

        public long Id => WeaponAttachmentComboId;
        public string Value => $"{Attachment1Id}{Attachment2Id}{Attachment3Id}";
    }
}
