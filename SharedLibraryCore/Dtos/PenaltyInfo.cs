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
        public ulong OffenderNetworkId { get; set; }
        public string OffenderIPAddress { get; set; }
        public string PunisherName { get; set; }
        public int PunisherId { get; set; }
        public ulong PunisherNetworkId { get; set; }
        public string PunisherIPAddress { get; set; }
        public string PunisherLevel { get; set; }
        public int PunisherLevelId { get; set; }
        public string Offense { get; set; }
        public string AutomatedOffense { get; set; }
        public string PenaltyType { get; set; }
        public string TimePunished { get; set; }
        public string TimeRemaining { get; set; }
        public bool Expired { get; set; }
    }
}
