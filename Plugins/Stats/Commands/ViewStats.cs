using SharedLibraryCore;
using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models.Client.Stats;
using Microsoft.EntityFrameworkCore;
using IW4MAdmin.Plugins.Stats.Helpers;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Commands;

namespace IW4MAdmin.Plugins.Stats.Commands
{
    public class ViewStatsCommand : Command
    {
        private readonly IDatabaseContextFactory _contextFactory;

        public ViewStatsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
            IDatabaseContextFactory contextFactory) : base(config, translationLookup)
        {
            Name = "stats";
            Description = translationLookup["PLUGINS_STATS_COMMANDS_VIEW_DESC"];
            Alias = "xlrstats";
            Permission = EFClient.Permission.User;
            RequiresTarget = false;
            Arguments = new CommandArgument[]
            {
                new CommandArgument()
                {
                    Name = "player",
                    Required = false
                }
            };
            
            _contextFactory = contextFactory;
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            string statLine;
            EFClientStatistics pStats = null;

            if (E.Data.Length > 0 && E.Target == null)
            {
                E.Target = E.Owner.GetClientByName(E.Data).FirstOrDefault();

                if (E.Target == null)
                {
                    E.Origin.Tell(_translationLookup["PLUGINS_STATS_COMMANDS_VIEW_FAIL"]);
                }
            }

            var serverId = StatManager.GetIdForServer(E.Owner);

            var totalRankedPlayers = await Plugin.Manager.GetTotalRankedPlayers(serverId);

            // getting stats for a particular client
            if (E.Target != null)
            {
                var performanceRanking = await Plugin.Manager.GetClientOverallRanking(E.Target.ClientId, serverId);
                var performanceRankingString = performanceRanking == 0
                    ? _translationLookup["WEBFRONT_STATS_INDEX_UNRANKED"]
                    : $"{_translationLookup["WEBFRONT_STATS_INDEX_RANKED"]} #{performanceRanking}/{totalRankedPlayers}";

                // target is currently connected so we want their cached stats if they exist
                if (E.Owner.GetClientsAsList().Any(client => client.Equals(E.Target)))
                {
                    pStats = E.Target.GetAdditionalProperty<EFClientStatistics>(StatManager.CLIENT_STATS_KEY);
                }

                // target is not connected so we want to look up via database
                if (pStats == null)
                {
                    await using var context = _contextFactory.CreateContext(false);
                    pStats = (await context.Set<EFClientStatistics>()
                        .FirstOrDefaultAsync(c => c.ServerId == serverId && c.ClientId == E.Target.ClientId));
                }

                // if it's still null then they've not gotten a kill or death yet
                statLine = pStats == null
                    ? _translationLookup["PLUGINS_STATS_COMMANDS_NOTAVAILABLE"]
                    : $"^5{pStats.Kills} ^7{_translationLookup["PLUGINS_STATS_TEXT_KILLS"]} | ^5{pStats.Deaths} ^7{_translationLookup["PLUGINS_STATS_TEXT_DEATHS"]} | ^5{pStats.KDR} ^7KDR | ^5{pStats.Performance} ^7{_translationLookup["PLUGINS_STATS_COMMANDS_PERFORMANCE"].ToUpper()} | {performanceRankingString}";
            }

            // getting self stats
            else
            {
                var performanceRanking = await Plugin.Manager.GetClientOverallRanking(E.Origin.ClientId, serverId);
                var performanceRankingString = performanceRanking == 0
                    ? _translationLookup["WEBFRONT_STATS_INDEX_UNRANKED"]
                    : $"{_translationLookup["WEBFRONT_STATS_INDEX_RANKED"]} #{performanceRanking}/{totalRankedPlayers}";

                // check if current client is connected to the server
                if (E.Owner.GetClientsAsList().Any(client => client.Equals(E.Origin)))
                {
                    pStats = E.Origin.GetAdditionalProperty<EFClientStatistics>(StatManager.CLIENT_STATS_KEY);
                }

                // happens if the user has not gotten a kill/death since connecting
                if (pStats == null)
                {
                    await using var context = _contextFactory.CreateContext(false);
                    pStats = (await context.Set<EFClientStatistics>()
                        .FirstOrDefaultAsync(c => c.ServerId == serverId && c.ClientId == E.Origin.ClientId));
                }

                // if it's still null then they've not gotten a kill or death yet
                statLine = pStats == null
                    ? _translationLookup["PLUGINS_STATS_COMMANDS_NOTAVAILABLE"]
                    : $"^5{pStats.Kills} ^7{_translationLookup["PLUGINS_STATS_TEXT_KILLS"]} | ^5{pStats.Deaths} ^7{_translationLookup["PLUGINS_STATS_TEXT_DEATHS"]} | ^5{pStats.KDR} ^7KDR | ^5{pStats.Performance} ^7{_translationLookup["PLUGINS_STATS_COMMANDS_PERFORMANCE"].ToUpper()} | {performanceRankingString}";
            }

            if (E.Message.IsBroadcastCommand(_config.BroadcastCommandPrefix))
            {
                var name = E.Target == null ? E.Origin.Name : E.Target.Name;
                E.Owner.Broadcast(_translationLookup["PLUGINS_STATS_COMMANDS_VIEW_SUCCESS"].FormatExt(name));
                E.Owner.Broadcast(statLine);
            }

            else
            {
                if (E.Target != null)
                {
                    E.Origin.Tell(_translationLookup["PLUGINS_STATS_COMMANDS_VIEW_SUCCESS"].FormatExt(E.Target.Name));
                }

                E.Origin.Tell(statLine);
            }
        }
    }
}