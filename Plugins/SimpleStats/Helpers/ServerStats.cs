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
        public EFServer Server { get; private set; }

        public ServerStats(EFServer sv)
        {
            PlayerStats = new Dictionary<int, EFClientStatistics>();
            Server = sv;
        }
    }
}
