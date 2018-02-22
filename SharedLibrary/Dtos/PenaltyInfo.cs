using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Dtos
{
    public class PenaltyInfo
    {
        public string OffenderName { get; set; }
        public int OffenderId { get; set; }
        public string PunisherName { get; set; }
        public int PunisherId { get; set; }
        public string Offense { get; set; }
        public string Type { get; set; }
        public string TimePunished { get; set; }
        public string TimeRemaining { get; set; }
    }
}
