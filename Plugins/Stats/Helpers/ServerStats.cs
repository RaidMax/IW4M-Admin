using IW4MAdmin.Plugins.Stats.Cheat;
using IW4MAdmin.Plugins.Stats.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace IW4MAdmin.Plugins.Stats.Helpers
{
    class ServerStats    {
        public ConcurrentDictionary<int, EFClientStatistics> PlayerStats { get; set; }
        public ConcurrentDictionary<int, Detection> PlayerDetections { get; set; }
        public IList<EFClientKill> HitCache { get; private set; }
        public EFServerStatistics ServerStatistics { get; private set; }
        public EFServer Server { get; private set; }
        public bool IsTeamBased { get; set; }

        public ServerStats(EFServer sv, EFServerStatistics st)
        {
            PlayerStats = new ConcurrentDictionary<int, EFClientStatistics>();
            PlayerDetections = new ConcurrentDictionary<int, Detection>();
            HitCache = new List<EFClientKill>();
            ServerStatistics = st;
            Server = sv;
        }

        public int TeamCount(IW4Info.Team teamName)
        {
            if (PlayerStats.Count(p => p.Value.Team == IW4Info.Team.None) / (double)PlayerStats.Count <= 0.25)
            {
                return IsTeamBased ? Math.Max(PlayerStats.Count(p => p.Value.Team == teamName), 1) : Math.Max(PlayerStats.Count - 1, 1);
            }
            
            else
            {
                return IsTeamBased ? (int)Math.Max(Math.Floor(PlayerStats.Count / 2.0), 1) : Math.Max(PlayerStats.Count - 1, 1);
            }
        }
    }
}
