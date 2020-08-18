﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

using SharedLibraryCore;
using IW4MAdmin.Plugins.Stats.Models;
using SharedLibraryCore.Database;
using System.Collections.Generic;
using SharedLibraryCore.Database.Models;
using IW4MAdmin.Plugins.Stats.Helpers;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.Stats.Commands
{
    class MostPlayedCommand : Command
    {
        public static async Task<List<string>> GetMostPlayed(Server s, ITranslationLookup translationLookup)
        {
            long serverId = StatManager.GetIdForServer(s);

            List<string> mostPlayed = new List<string>()
            {
                $"^5--{translationLookup["PLUGINS_STATS_COMMANDS_MOSTPLAYED_TEXT"]}--"
            };

            using (var db = new DatabaseContext(true))
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
                               orderby stats.TimePlayed descending
                               select new
                               {
                                   alias.Name,
                                   client.TotalConnectionTime,
                                   stats.Kills
                               })
                                .Take(5);

                var iqList = await iqStats.ToListAsync();

                mostPlayed.AddRange(iqList.Select(stats => translationLookup["COMMANDS_MOST_PLAYED_FORMAT"].FormatExt(stats.Name, (DateTime.UtcNow - DateTime.UtcNow.AddSeconds(-stats.TotalConnectionTime)).HumanizeForCurrentCulture())));
            }


            return mostPlayed;
        }

        private readonly CommandConfiguration _config;

        public MostPlayedCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup) 
        {
            Name = "mostplayed";
            Description = translationLookup["PLUGINS_STATS_COMMANDS_MOSTPLAYED_DESC"];
            Alias = "mp";
            Permission = EFClient.Permission.User;
            RequiresTarget = false;

            _config = config;
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var topStats = await GetMostPlayed(E.Owner, _translationLookup);
            if (!E.Message.IsBroadcastCommand(_config.BroadcastCommandPrefix))
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
