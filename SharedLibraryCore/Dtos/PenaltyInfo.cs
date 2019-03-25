using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Dtos
{
    public class PenaltyInfo : SharedInfo
    {
        public string OffenderName { get; set; }
        public int OffenderId { get; set; }
        public string PunisherName { get; set; }
        public int PunisherId { get; set; }
        public string PunisherLevel { get; set; }
        public int PunisherLevelId { get; set; }
        public string Offense { get; set; }
        public string AutomatedOffense { get; set; }
        public string Type { get; set; }
        public string TimePunished { get; set; }
        public string TimeRemaining { get; set; }
        public bool Expired { get; set; }
        public string IPAddress { get; set; }
        public ulong NetworkId { get; set; }
    }
}
