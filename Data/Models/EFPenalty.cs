using Data.Models.Client;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Models
{
    public class EFPenalty : SharedEntity
    {
        public enum PenaltyType
        {
            Report,
            Warning,
            Flag,
            Kick,
            TempBan,
            Ban,
            Unban,
            Any,
            Unflag,
            Other = 100
        }

        [Key]
        public int PenaltyId { get; set; }
        public int? LinkId { get; set; }
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
        public PenaltyType Type { get; set; }
    }
}
