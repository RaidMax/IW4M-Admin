using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using SharedLibraryCore;
using IW4MAdmin.Plugins.Stats.Models;
using System.Collections.Generic;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using IW4MAdmin.Plugins.Stats.Config;
using IW4MAdmin.Plugins.Stats.Helpers;

namespace IW4MAdmin.Plugins.Stats.Commands
{
    class MostKillsCommand : Command
    {
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly CommandConfiguration _config;

        public MostKillsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
            IDatabaseContextFactory contextFactory) : base(config, translationLookup)
        {
            Name = "mostkills";
            Description = translationLookup["PLUGINS_STATS_COMMANDS_MOSTKILLS_DESC"];
            Alias = "mk";
            Permission = EFClient.Permission.User;

            _contextFactory = contextFactory;
            _config = config;
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var mostKills = await GetMostKills(StatManager.GetIdForServer(E.Owner), Plugin.Config.Configuration(),
                _contextFactory, _translationLookup);
            if (!E.Message.IsBroadcastCommand(_config.BroadcastCommandPrefix))
            {
                foreach (var stat in mostKills)
                {
                    E.Origin.Tell(stat);
                }
            }

            else
            {
                foreach (var stat in mostKills)
                {
                    E.Owner.Broadcast(stat);
                }
            }
        }

        public static async Task<IEnumerable<string>> GetMostKills(long? serverId, StatsConfiguration config,
            IDatabaseContextFactory contextFactory, ITranslationLookup translationLookup)
        {
            await using var ctx = contextFactory.CreateContext(enableTracking: false);
            var dayInPast = DateTime.UtcNow.AddDays(-config.MostKillsMaxInactivityDays);

            var iqStats = (from stats in ctx.Set<EFClientStatistics>()
                    join client in ctx.Clients
                        on stats.ClientId equals client.ClientId
                    join alias in ctx.Aliases
                        on client.CurrentAliasId equals alias.AliasId
                    where stats.ServerId == serverId
                    where client.Level != EFClient.Permission.Banned
                    where client.LastConnection >= dayInPast
                    orderby stats.Kills descending
                    select new
                    {
                        alias.Name,
                        stats.Kills
                    })
                .Take(config.MostKillsClientLimit);

            var iqList = await iqStats.ToListAsync();

            return iqList.Select((stats, index) => translationLookup["PLUGINS_STATS_COMMANDS_MOSTKILLS_FORMAT"]
                    .FormatExt(index + 1, stats.Name, stats.Kills))
                .Prepend(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_MOSTKILLS_HEADER"]);
        }
    }
}