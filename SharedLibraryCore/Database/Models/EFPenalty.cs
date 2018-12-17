using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLibraryCore.Database.Models
{
    public class EFPenalty : SharedEntity
    {
        [Key]
        public int PenaltyId { get; set; }
        [Required]
        public int LinkId { get; set; }
        [ForeignKey("LinkId")]
        public virtual EFAliasLink Link { get; set; }
        [Required]
        public int OffenderId { get; set; }
        [ForeignKey("OffenderId")]
        public virtual EFClient Offender { get; set; }
        [Required]
        public int PunisherId { get; set; }
        [ForeignKey("PunisherId")]
        public virtual EFClient Punisher { get; set; }
        [Required]
        public DateTime When { get; set; }
        [Required]
        public DateTime? Expires { get; set; }
        [Required]
        public string Offense { get; set; }
        public string AutomatedOffense { get; set; }
        [Required]
        public bool IsEvadedOffense { get; set; }
        public Objects.Penalty.PenaltyType Type { get; set; }
    }
}
