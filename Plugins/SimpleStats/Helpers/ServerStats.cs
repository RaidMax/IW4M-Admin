using SharedLibrary;
using StatsPlugin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin.Helpers
{
    public class ServerStats
    {
        public Dictionary<int, EFClientStatistics> PlayerStats { get; set; }
        public EFServerStatistics ServerStatistics { get; private set; }
        public EFServer Server { get; private set; }

        public ServerStats(EFServer sv, EFServerStatistics st)
        {
            PlayerStats = new Dictionary<int, EFClientStatistics>();
            ServerStatistics = st;
            Server = sv;
        }
    }
}
