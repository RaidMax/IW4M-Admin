using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Database.Models
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
        public DateTime Expires { get; set; }
        [Required]
        public string Offense { get; set; }
        public Objects.Penalty.PenaltyType Type { get; set; }
    }
}
