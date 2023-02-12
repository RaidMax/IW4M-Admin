using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using SharedLibraryCore;
using System.Collections.Generic;
using Data.Abstractions;
using Data.Models.Client.Stats;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using IW4MAdmin.Plugins.Stats.Helpers;
using Stats.Config;

namespace IW4MAdmin.Plugins.Stats.Commands;

class MostKillsCommand : Command
{
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly StatsConfiguration _statsConfig;

    public MostKillsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        IDatabaseContextFactory contextFactory, StatsConfiguration statsConfig) : base(config, translationLookup)
    {
        Name = "mostkills";
        Description = translationLookup["PLUGINS_STATS_COMMANDS_MOSTKILLS_DESC"];
        Alias = "mk";
        Permission = EFClient.Permission.User;

        _contextFactory = contextFactory;
        _statsConfig = statsConfig;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var mostKills = await GetMostKills(StatManager.GetIdForServer(gameEvent.Owner), _statsConfig,
            _contextFactory, _translationLookup);
        if (!gameEvent.Message.IsBroadcastCommand(_config.BroadcastCommandPrefix))
        {
            await gameEvent.Origin.TellAsync(mostKills, gameEvent.Owner.Manager.CancellationToken);
        }

        else
        {
            foreach (var stat in mostKills)
            {
                await gameEvent.Owner.Broadcast(stat).WaitAsync(Utilities.DefaultCommandTimeout,
                    gameEvent.Owner.Manager.CancellationToken);
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

        return iqList.Select((stats, index) => translationLookup["PLUGINS_STATS_COMMANDS_MOSTKILLS_FORMAT_V2"]
                .FormatExt(index + 1, stats.Name, stats.Kills))
            .Prepend(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_MOSTKILLS_HEADER"]);
    }
}
