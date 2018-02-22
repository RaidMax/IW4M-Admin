using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Dtos
{
    public class PlayerInfo
    {
        public string Name { get; set; }
        public int ClientId { get; set; }
        public string Level { get; set; }
        public string IPAddress { get; set; }
        public long NetworkId { get; set; }
        public List<string> Aliases { get; set; }
        public List<string> IPs { get; set; }
        public int ConnectionCount { get; set; }
        public string LastSeen { get; set; }
        public string FirstSeen { get; set; }
        public string TimePlayed { get; set; }
        public bool Authenticated { get; set; }
        public List<ProfileMeta> Meta { get; set; }
    }
}
