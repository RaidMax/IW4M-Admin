using SharedLibraryCore;
using IW4MAdmin.Plugins.Stats.Models;
using System.Linq;
using System.Threading.Tasks;
using SharedLibraryCore.Database;
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
        public ViewStatsCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config, translationLookup)
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
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            string statLine;
            EFClientStatistics pStats;

            if (E.Data.Length > 0 && E.Target == null)
            {
                E.Target = E.Owner.GetClientByName(E.Data).FirstOrDefault();

                if (E.Target == null)
                {
                    E.Origin.Tell(_translationLookup["PLUGINS_STATS_COMMANDS_VIEW_FAIL"]);
                }
            }

            long serverId = StatManager.GetIdForServer(E.Owner);


            if (E.Target != null)
            {
                int performanceRanking = await StatManager.GetClientOverallRanking(E.Target.ClientId);
                string performanceRankingString = performanceRanking == 0 ? _translationLookup["WEBFRONT_STATS_INDEX_UNRANKED"] : $"{_translationLookup["WEBFRONT_STATS_INDEX_RANKED"]} #{performanceRanking}";

                if (E.Owner.GetClientsAsList().Any(_client => _client.Equals(E.Target)))
                {
                    pStats = E.Target.GetAdditionalProperty<EFClientStatistics>(StatManager.CLIENT_STATS_KEY);
                }

                else
                {
                    using (var ctx = new DatabaseContext(true))
                    {
                        pStats = (await ctx.Set<EFClientStatistics>().FirstAsync(c => c.ServerId == serverId && c.ClientId == E.Target.ClientId));
                    }
                }
                statLine = $"^5{pStats.Kills} ^7{_translationLookup["PLUGINS_STATS_TEXT_KILLS"]} | ^5{pStats.Deaths} ^7{_translationLookup["PLUGINS_STATS_TEXT_DEATHS"]} | ^5{pStats.KDR} ^7KDR | ^5{pStats.Performance} ^7{_translationLookup["PLUGINS_STATS_COMMANDS_PERFORMANCE"].ToUpper()} | {performanceRankingString}";
            }

            else
            {
                int performanceRanking = await StatManager.GetClientOverallRanking(E.Origin.ClientId);
                string performanceRankingString = performanceRanking == 0 ? _translationLookup["WEBFRONT_STATS_INDEX_UNRANKED"] : $"{_translationLookup["WEBFRONT_STATS_INDEX_RANKED"]} #{performanceRanking}";

                if (E.Owner.GetClientsAsList().Any(_client => _client.Equals(E.Origin)))
                {
                    pStats = E.Origin.GetAdditionalProperty<EFClientStatistics>(StatManager.CLIENT_STATS_KEY);
                }

                else
                {
                    using (var ctx = new DatabaseContext(true))
                    {
                        pStats = (await ctx.Set<EFClientStatistics>().FirstAsync(c => c.ServerId == serverId && c.ClientId == E.Origin.ClientId));
                    }
                }
                statLine = $"^5{pStats.Kills} ^7{_translationLookup["PLUGINS_STATS_TEXT_KILLS"]} | ^5{pStats.Deaths} ^7{_translationLookup["PLUGINS_STATS_TEXT_DEATHS"]} | ^5{pStats.KDR} ^7KDR | ^5{pStats.Performance} ^7{_translationLookup["PLUGINS_STATS_COMMANDS_PERFORMANCE"].ToUpper()} | {performanceRankingString}";
            }

            if (E.Message.IsBroadcastCommand())
            {
                string name = E.Target == null ? E.Origin.Name : E.Target.Name;
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
