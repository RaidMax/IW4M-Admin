using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database.Models
{
    public class Penalty
    {
        [Key]
        public int PenaltyId { get; set; }
        public int OffenderId { get; set; }
        public Client Offender { get; set; }
        public int PunisherId { get; set; }
        public Client Punisher { get; set; }
        public DateTime When { get; set; }
        public DateTime Expires { get; set; }
        public SharedLibrary.Penalty.Type Type { get; set; }
    }
}
