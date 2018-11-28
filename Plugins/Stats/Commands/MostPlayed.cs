using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

using SharedLibraryCore;
using SharedLibraryCore.Objects;
using IW4MAdmin.Plugins.Stats.Models;
using SharedLibraryCore.Database;
using System.Collections.Generic;
using SharedLibraryCore.Database.Models;
using IW4MAdmin.Plugins.Stats.Helpers;

namespace IW4MAdmin.Plugins.Stats.Commands
{
    class MostPlayed : Command
    {
        public static async Task<List<string>> GetMostPlayed(Server s)
        {
            long serverId = await StatManager.GetIdForServer(s);

            List<string> mostPlayed = new List<string>()
            {
                $"^5--{Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_MOSTPLAYED_TEXT"]}--"
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
                               where client.Level != EFClient.Permission.Banned
                               where client.LastConnection >= thirtyDaysAgo
                               orderby stats.Kills descending
                               select new
                               {
                                   alias.Name,
                                   client.TotalConnectionTime,
                                   stats.Kills
                               })
                                .Take(5);

                var iqList = await iqStats.ToListAsync();

                mostPlayed.AddRange(iqList.Select(stats =>
                $"^3{stats.Name}^7 - ^5{stats.Kills} ^7{Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_KILLS"]} | ^5{Utilities.GetTimePassed(DateTime.UtcNow.AddSeconds(-stats.TotalConnectionTime), false)} ^7{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_PLAYER"].ToLower()}"));
            }


            return mostPlayed;
        }

        public MostPlayed() : base("mostplayed", Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_MOSTPLAYED_DESC"], "mp", EFClient.Permission.User, false) { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var topStats = await GetMostPlayed(E.Owner);
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
