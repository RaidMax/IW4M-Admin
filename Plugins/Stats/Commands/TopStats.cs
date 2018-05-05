using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

using SharedLibraryCore;
using SharedLibraryCore.Objects;
using SharedLibraryCore.Services;
using IW4MAdmin.Plugins.Stats.Models;
using SharedLibraryCore.Database;
using System.Collections.Generic;

namespace IW4MAdmin.Plugins.Stats.Commands
{
    class TopStats : Command
    {

        public static async Task<List<string>> GetTopStats(Server s)
        {
            int serverId = s.GetHashCode();
            List<string> topStatsText = new List<string>()
            {
                $"^5--{Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_TOP_TEXT"]}--"
            };

            using (var db = new DatabaseContext())
            {
                db.ChangeTracker.AutoDetectChangesEnabled = false;
                db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

                var thirtyDaysAgo = DateTime.UtcNow.AddMonths(-1);

                var iqStats = (from stats in db.Set<EFClientStatistics>()
                               join client in db.Clients
                               on stats.ClientId equals client.ClientId
                               join alias in db.Aliases
                               on client.CurrentAliasId equals alias.AliasId
                               where stats.ServerId == serverId
                               where stats.TimePlayed >= 3600
                               where client.Level != Player.Permission.Banned
                               where client.LastConnection >= thirtyDaysAgo
                               orderby stats.Skill descending
                               select $"^3{client.Name}^7 - ^5{stats.KDR} ^7KDR | ^5{stats.Skill} ^7{Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_SKILL"]}")
                              .Take(5);

                topStatsText.AddRange(await iqStats.ToListAsync());
            }

            // no one qualified
            if (topStatsText.Count == 0)
            {
                topStatsText = new List<string>()
                {
                    Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_NOQUALIFY"]
                };
            }

            return topStatsText;
        }

        public TopStats() : base("topstats", Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_TOP_DESC"], "ts", Player.Permission.User, false) { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var topStats = await GetTopStats(E.Owner);
            if (!E.Message.IsBroadcastCommand())
            {
                foreach (var stat in topStats)
                    await E.Origin.Tell(stat);
            }
            else
            {
                foreach (var stat in topStats)
                    await E.Owner.Broadcast(stat);
            }
        }
    }
}
