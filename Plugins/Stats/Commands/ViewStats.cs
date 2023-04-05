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
        private readonly StatManager _statManager;

        public ViewStatsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
            IDatabaseContextFactory contextFactory, StatManager statManager) : base(config, translationLookup)
        {
            Name = "stats";
            Description = translationLookup["PLUGINS_STATS_COMMANDS_VIEW_DESC"];
            Alias = "xlrstats";
            Permission = EFClient.Permission.User;
            RequiresTarget = false;
            Arguments = new []
            {
                new CommandArgument
                {
                    Name = "player",
                    Required = false
                }
            };
            
            _contextFactory = contextFactory;
            _statManager = statManager;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            string statLine;
            EFClientStatistics pStats = null;

            if (gameEvent.Data.Length > 0 && gameEvent.Target == null)
            {
                gameEvent.Target = gameEvent.Owner.GetClientByName(gameEvent.Data).FirstOrDefault();

                if (gameEvent.Target == null)
                {
                    gameEvent.Origin.Tell(_translationLookup["PLUGINS_STATS_COMMANDS_VIEW_FAIL"]);
                }
            }

            var serverId = (gameEvent.Owner as IGameServer).LegacyDatabaseId;
            var totalRankedPlayers = await _statManager.GetTotalRankedPlayers(serverId);

            // getting stats for a particular client
            if (gameEvent.Target != null)
            {
                var performanceRanking = await _statManager.GetClientOverallRanking(gameEvent.Target.ClientId, serverId);
                var performanceRankingString = performanceRanking == 0
                    ? _translationLookup["WEBFRONT_STATS_INDEX_UNRANKED"]
                    : $"{_translationLookup["WEBFRONT_STATS_INDEX_RANKED"]} (Color::Accent)#{performanceRanking}/{totalRankedPlayers}";

                // target is currently connected so we want their cached stats if they exist
                if (gameEvent.Owner.GetClientsAsList().Any(client => client.Equals(gameEvent.Target)))
                {
                    pStats = gameEvent.Target.GetAdditionalProperty<EFClientStatistics>(StatManager.CLIENT_STATS_KEY);
                }

                // target is not connected so we want to look up via database
                if (pStats == null)
                {
                    await using var context = _contextFactory.CreateContext(false);
                    pStats = await context.Set<EFClientStatistics>()
                        .FirstOrDefaultAsync(c => c.ServerId == serverId && c.ClientId == gameEvent.Target.ClientId);
                }

                // if it's still null then they've not gotten a kill or death yet
                statLine = pStats == null
                    ? _translationLookup["PLUGINS_STATS_COMMANDS_NOTAVAILABLE"]
                    : _translationLookup["COMMANDS_VIEW_STATS_RESULT"].FormatExt(pStats.Kills, pStats.Deaths,
                        pStats.KDR, pStats.Performance, performanceRankingString);
            }

            // getting self stats
            else
            {
                var performanceRanking = await _statManager.GetClientOverallRanking(gameEvent.Origin.ClientId, serverId);
                var performanceRankingString = performanceRanking == 0
                    ? _translationLookup["WEBFRONT_STATS_INDEX_UNRANKED"]
                    : $"{_translationLookup["WEBFRONT_STATS_INDEX_RANKED"]} (Color::Accent)#{performanceRanking}/{totalRankedPlayers}";

                // check if current client is connected to the server
                if (gameEvent.Owner.GetClientsAsList().Any(client => client.Equals(gameEvent.Origin)))
                {
                    pStats = gameEvent.Origin.GetAdditionalProperty<EFClientStatistics>(StatManager.CLIENT_STATS_KEY);
                }

                // happens if the user has not gotten a kill/death since connecting
                if (pStats == null)
                {
                    await using var context = _contextFactory.CreateContext(false);
                    pStats = (await context.Set<EFClientStatistics>()
                        .FirstOrDefaultAsync(c => c.ServerId == serverId && c.ClientId == gameEvent.Origin.ClientId));
                }

                // if it's still null then they've not gotten a kill or death yet
                statLine = pStats == null
                    ? _translationLookup["PLUGINS_STATS_COMMANDS_NOTAVAILABLE"]
                    : _translationLookup["COMMANDS_VIEW_STATS_RESULT"].FormatExt(pStats.Kills, pStats.Deaths,
                        pStats.KDR, pStats.Performance, performanceRankingString);
            }

            if (gameEvent.Message.IsBroadcastCommand(_config.BroadcastCommandPrefix))
            {
                var name = gameEvent.Target == null ? gameEvent.Origin.Name : gameEvent.Target.Name;
                gameEvent.Owner.Broadcast(_translationLookup["PLUGINS_STATS_COMMANDS_VIEW_SUCCESS"].FormatExt(name));
                gameEvent.Owner.Broadcast(statLine);
            }

            else
            {
                if (gameEvent.Target != null)
                {
                    gameEvent.Origin.Tell(_translationLookup["PLUGINS_STATS_COMMANDS_VIEW_SUCCESS"].FormatExt(gameEvent.Target.Name));
                }

                gameEvent.Origin.Tell(statLine);
            }
        }
    }
}
