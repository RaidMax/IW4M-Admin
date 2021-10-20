using SharedLibraryCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Data.Models.Client;
using Data.Models.Client.Stats;
using Data.Models.Server;

namespace IW4MAdmin.Plugins.Stats.Helpers
{
    class ServerStats
    {
        public IList<EFClientKill> HitCache { get; private set; }
        public EFServerStatistics ServerStatistics { get; private set; }
        public EFServer Server { get; private set; }
        private readonly Server _server;
        public bool IsTeamBased { get; set; }
        public SemaphoreSlim OnSaving { get; private set; }

        public ServerStats(EFServer sv, EFServerStatistics st, Server server)
        {
            HitCache = new List<EFClientKill>();
            ServerStatistics = st;
            Server = sv;
            _server = server;
            OnSaving = new SemaphoreSlim(1, 1);
        }

        ~ServerStats()
        {
            OnSaving.Dispose();
        }

        public int TeamCount(IW4Info.Team teamName)
        {
            var PlayerStats = _server.GetClientsAsList()
                .Select(_c => _c.GetAdditionalProperty<EFClientStatistics>(StatManager.CLIENT_STATS_KEY))
                .Where(_c => _c != null);

            if (PlayerStats.Count(p => p.Team == (int)IW4Info.Team.None) / (double)PlayerStats.Count() <= 0.25)
            {
                return IsTeamBased ? Math.Max(PlayerStats.Count(p => p.Team == (int)teamName), 1) : Math.Max(PlayerStats.Count() - 1, 1);
            }

            else
            {
                return IsTeamBased ? (int)Math.Max(Math.Floor(PlayerStats.Count() / 2.0), 1) : Math.Max(PlayerStats.Count() - 1, 1);
            }
        }
    }
}
