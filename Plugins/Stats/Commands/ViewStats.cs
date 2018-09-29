using SharedLibraryCore;
using SharedLibraryCore.Objects;
using SharedLibraryCore.Services;
using IW4MAdmin.Plugins.Stats.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IW4MAdmin.Plugins.Stats.Commands
{
    public class CViewStats : Command
    {
        public CViewStats() : base("stats", Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_VIEW_DESC"], "xlrstats", Player.Permission.User, false, new CommandArgument[]
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

            var clientStats = new GenericRepository<EFClientStatistics>();
            int serverId = E.Owner.GetHashCode();

            if (E.Target != null)
            {
                pStats = (await clientStats.FindAsync(c => c.ServerId == serverId && c.ClientId == E.Target.ClientId)).First();
                statLine = $"^5{pStats.Kills} ^7{loc["PLUGINS_STATS_TEXT_KILLS"]} | ^5{pStats.Deaths} ^7{loc["PLUGINS_STATS_TEXT_DEATHS"]} | ^5{pStats.KDR} ^7KDR | ^5{pStats.Performance} ^7{loc["PLUGINS_STATS_COMMANDS_PERFORMANCE"].ToUpper()}";
            }

            else
            {
                pStats = (await clientStats.FindAsync(c => c.ServerId == serverId && c.ClientId == E.Origin.ClientId)).First();
                statLine = $"^5{pStats.Kills} ^7{loc["PLUGINS_STATS_TEXT_KILLS"]} | ^5{pStats.Deaths} ^7{loc["PLUGINS_STATS_TEXT_DEATHS"]} | ^5{pStats.KDR} ^7KDR | ^5{pStats.Performance} ^7{loc["PLUGINS_STATS_COMMANDS_PERFORMANCE"].ToUpper()}";
            }

            if (E.Message.IsBroadcastCommand())
            {
                string name = E.Target == null ? E.Origin.Name : E.Target.Name;
                E.Owner.Broadcast($"{loc["PLUGINS_STATS_COMMANDS_VIEW_SUCCESS"]} ^5{name}^7");
                E.Owner.Broadcast(statLine);
            }

            else
            {
                if (E.Target != null)
                {
                    E.Origin.Tell($"{loc["PLUGINS_STATS_COMMANDS_VIEW_SUCCESS"]} ^5{E.Target.Name}^7");
                }

                E.Origin.Tell(statLine);
            }
        }
    }
}
