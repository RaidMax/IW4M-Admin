using SharedLibrary;
using StatsPlugin.Cheat;
using StatsPlugin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin.Helpers
{
    class ServerStats    {
        public Dictionary<int, EFClientStatistics> PlayerStats { get; set; }
        public Dictionary<int, Detection> PlayerDetections { get; set; }
        public EFServerStatistics ServerStatistics { get; private set; }
        public EFServer Server { get; private set; }

        public ServerStats(EFServer sv, EFServerStatistics st)
        {
            PlayerStats = new Dictionary<int, EFClientStatistics>();
            PlayerDetections = new Dictionary<int, Detection>();
            ServerStatistics = st;
            Server = sv;
        }
    }
}
