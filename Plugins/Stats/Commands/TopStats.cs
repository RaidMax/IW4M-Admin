using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

using SharedLibraryCore;
using SharedLibraryCore.Services;
using IW4MAdmin.Plugins.Stats.Models;
using SharedLibraryCore.Database;
using System.Collections.Generic;
using SharedLibraryCore.Database.Models;
using IW4MAdmin.Plugins.Stats.Helpers;

namespace IW4MAdmin.Plugins.Stats.Commands
{
    class TopStats : Command
    {
        public static async Task<List<string>> GetTopStats(Server s)
        {
            long serverId = await StatManager.GetIdForServer(s); 
            List<string> topStatsText = new List<string>()
            {
                $"^5--{Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_TOP_TEXT"]}--"
            };

            using (var db = new DatabaseContext(true))
            {
                var fifteenDaysAgo = DateTime.UtcNow.AddDays(-15);

                var iqStats = (from stats in db.Set<EFClientStatistics>()
                               join client in db.Clients
                               on stats.ClientId equals client.ClientId
                               join alias in db.Aliases
                               on client.CurrentAliasId equals alias.AliasId
                               where stats.ServerId == serverId
                               where stats.TimePlayed >= Plugin.Config.Configuration().TopPlayersMinPlayTime
                               where client.Level != EFClient.Permission.Banned
                               where client.LastConnection >= fifteenDaysAgo
                               orderby stats.Performance descending
                               select new
                               {
                                   stats.KDR,
                                   stats.Performance,
                                   alias.Name
                               })
                              .Take(5);

#if DEBUG == true
                var statsSql = iqStats.ToSql();
#endif

                var statsList = (await iqStats.ToListAsync())
                    .Select(stats => $"^3{stats.Name}^7 - ^5{stats.KDR} ^7{Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_KDR"]} | ^5{stats.Performance} ^7{Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_PERFORMANCE"]}");

                topStatsText.AddRange(statsList);
            }

            // no one qualified
            if (topStatsText.Count == 1)
            {
                topStatsText = new List<string>()
                {
                    Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_NOQUALIFY"]
                };
            }

            return topStatsText;
        }

        public TopStats() : base("topstats", Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_TOP_DESC"], "ts", EFClient.Permission.User, false) { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var topStats = await GetTopStats(E.Owner);
            if (!E.Message.IsBroadcastCommand())
            {
                foreach (var stat in topStats)
                {
                    E.Origin.Tell(stat);

                }
            }
            else
            {
                foreach (var stat in topStats)
                {
                    E.Owner.Broadcast(stat);
                }
            }
        }
    }
}
