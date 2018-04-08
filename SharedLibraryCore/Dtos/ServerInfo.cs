using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Dtos
{
    public class ServerInfo
    {
        public string Name { get; set; }
        public int Port { get; set; }
        public string Map { get; set; }
        public string GameType { get; set; }
        public int ClientCount { get; set; }
        public int MaxClients { get; set; }
        public ChatInfo[] ChatHistory { get; set; }
        public List<PlayerInfo> Players { get; set; }
        public Helpers.PlayerHistory[] PlayerHistory { get; set; }
        public int ID { get; set; }
        public bool Online { get; set; }
    }
}
