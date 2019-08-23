using SharedLibraryCore;
using SharedLibraryCore.Services;
using IW4MAdmin.Plugins.Stats.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharedLibraryCore.Database;
using Microsoft.EntityFrameworkCore;
using IW4MAdmin.Plugins.Stats.Helpers;
using SharedLibraryCore.Database.Models;

namespace IW4MAdmin.Plugins.Stats.Commands
{
    public class CViewStats : Command
    {
        public CViewStats() : base("stats", Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_VIEW_DESC"], "xlrstats", EFClient.Permission.User, false, new CommandArgument[]
            {
                new CommandArgument()
                {
                    Name = "player",
                    Required = false
                }
            })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;

            String statLine;
            EFClientStatistics pStats;

            if (E.Data.Length > 0 && E.Target == null)
            {
                E.Target = E.Owner.GetClientByName(E.Data).FirstOrDefault();

                if (E.Target == null)
                {
                    E.Origin.Tell(loc["PLUGINS_STATS_COMMANDS_VIEW_FAIL"]);
                }
            }

            long serverId = StatManager.GetIdForServer(E.Owner);

            using (var ctx = new DatabaseContext(disableTracking: true))
            {
                if (E.Target != null)
                {
                    int performanceRanking = await StatManager.GetClientOverallRanking(E.Target.ClientId);
                    string performanceRankingString = performanceRanking == 0 ? loc["WEBFRONT_STATS_INDEX_UNRANKED"] : $"{loc["WEBFRONT_STATS_INDEX_RANKED"]} #{performanceRanking}";

                    pStats = (await ctx.Set<EFClientStatistics>().FirstAsync(c => c.ServerId == serverId && c.ClientId == E.Target.ClientId));
                    statLine = $"^5{pStats.Kills} ^7{loc["PLUGINS_STATS_TEXT_KILLS"]} | ^5{pStats.Deaths} ^7{loc["PLUGINS_STATS_TEXT_DEATHS"]} | ^5{pStats.KDR} ^7KDR | ^5{pStats.Performance} ^7{loc["PLUGINS_STATS_COMMANDS_PERFORMANCE"].ToUpper()} | {performanceRankingString}";
                }

                else
                {
                    int performanceRanking = await StatManager.GetClientOverallRanking(E.Origin.ClientId);
                    string performanceRankingString = performanceRanking == 0 ? loc["WEBFRONT_STATS_INDEX_UNRANKED"] : $"{loc["WEBFRONT_STATS_INDEX_RANKED"]} #{performanceRanking}";

                    pStats = (await ctx.Set<EFClientStatistics>().FirstAsync((c => c.ServerId == serverId && c.ClientId == E.Origin.ClientId)));
                    statLine = $"^5{pStats.Kills} ^7{loc["PLUGINS_STATS_TEXT_KILLS"]} | ^5{pStats.Deaths} ^7{loc["PLUGINS_STATS_TEXT_DEATHS"]} | ^5{pStats.KDR} ^7KDR | ^5{pStats.Performance} ^7{loc["PLUGINS_STATS_COMMANDS_PERFORMANCE"].ToUpper()} | {performanceRankingString}";
                }
            }

            if (E.Message.IsBroadcastCommand())
            {
                string name = E.Target == null ? E.Origin.Name : E.Target.Name;
                E.Owner.Broadcast(loc["PLUGINS_STATS_COMMANDS_VIEW_SUCCESS"].FormatExt(name));
                E.Owner.Broadcast(statLine);
            }

            else
            {
                if (E.Target != null)
                {
                    E.Origin.Tell(loc["PLUGINS_STATS_COMMANDS_VIEW_SUCCESS"].FormatExt(E.Target.Name));
                }

                E.Origin.Tell(statLine);
            }
        }
    }
}
