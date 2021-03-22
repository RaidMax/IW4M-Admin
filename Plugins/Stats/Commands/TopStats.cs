using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

using SharedLibraryCore;
using System.Collections.Generic;
using Data.Abstractions;
using Data.Models.Client.Stats;
using SharedLibraryCore.Database.Models;
using IW4MAdmin.Plugins.Stats.Helpers;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.Stats.Commands
{
    class TopStats : Command
    {
        public static async Task<List<string>> GetTopStats(Server s, ITranslationLookup translationLookup)
        {
            long serverId = StatManager.GetIdForServer(s); 
            var topStatsText = new List<string>()
            {
                $"^5--{translationLookup["PLUGINS_STATS_COMMANDS_TOP_TEXT"]}--"
            };

            var stats = await Plugin.Manager.GetTopStats(0, 5, serverId);
            var statsList = stats.Select(stats => $"^3{stats.Name}^7 - ^5{stats.KDR} ^7{translationLookup["PLUGINS_STATS_TEXT_KDR"]} | ^5{stats.Performance} ^7{translationLookup["PLUGINS_STATS_COMMANDS_PERFORMANCE"]}");

            topStatsText.AddRange(statsList);

                // no one qualified
            if (topStatsText.Count == 1)
            {
                topStatsText = new List<string>()
                {
                    translationLookup["PLUGINS_STATS_TEXT_NOQUALIFY"]
                };
            }

            return topStatsText;
        }

        private readonly CommandConfiguration _config;
        private readonly IDatabaseContextFactory _contextFactory;

        public TopStats(CommandConfiguration config, ITranslationLookup translationLookup, 
            IDatabaseContextFactory contextFactory) : base(config, translationLookup) 
        {
            Name = "topstats";
            Description = translationLookup["PLUGINS_STATS_COMMANDS_TOP_DESC"];
            Alias = "ts";
            Permission = EFClient.Permission.User;
            RequiresTarget = false;

            _config = config;
            _contextFactory = contextFactory;
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var topStats = await GetTopStats(E.Owner, _translationLookup);
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
