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

            if (E.Target?.ClientNumber < 0)
            {
                await E.Origin.Tell(loc["PLUGINS_STATS_COMMANDS_VIEW_FAIL_INGAME"]);
                return;
            }

            if (E.Origin.ClientNumber < 0 && E.Target == null)
            {
                await E.Origin.Tell(loc["PLUGINS_STATS_COMMANDS_VIEW_FAIL_INGAME_SELF"]);
                return;
            }

            String statLine;
            EFClientStatistics pStats;

            if (E.Data.Length > 0 && E.Target == null)
            {
                await E.Origin.Tell(loc["PLUGINS_STATS_COMMANDS_VIEW_FAIL"]);
                return;
            }

            var clientStats = new GenericRepository<EFClientStatistics>();
            int serverId = E.Owner.GetHashCode();

            if (E.Target != null)
            {
                pStats = clientStats.Find(c => c.ServerId == serverId && c.ClientId == E.Target.ClientId).First();
                statLine = $"^5{pStats.Kills} ^7{loc["PLUGINS_STATS_TEXT_KILLS"]} | ^5{pStats.Deaths} ^7{loc["PLUGINS_STATS_TEXT_DEATHS"]} | ^5{pStats.KDR} ^7KDR | ^5{pStats.Skill} ^7{loc["PLUGINS_STATS_TEXT_SKILL"]}";
            }

            else
            {
                pStats = pStats = clientStats.Find(c => c.ServerId == serverId && c.ClientId == E.Origin.ClientId).First();
                statLine = $"^5{pStats.Kills} ^7{loc["PLUGINS_STATS_TEXT_KILLS"]} | ^5{pStats.Deaths} ^7{loc["PLUGINS_STATS_TEXT_DEATHS"]} | ^5{pStats.KDR} ^7KDR | ^5{pStats.Skill} ^7{loc["PLUGINS_STATS_TEXT_SKILL"]}";
            }

            if (E.Message.IsBroadcastCommand())
            {
                string name = E.Target == null ? E.Origin.Name : E.Target.Name;
                await E.Owner.Broadcast($"{loc["PLUGINS_STATS_COMMANDS_VIEW_SUCCESS"]} ^5{name}^7");
                await E.Owner.Broadcast(statLine);
            }

            else
            {
                if (E.Target != null)
                    await E.Origin.Tell($"{loc["PLUGINS_STATS_COMMANDS_VIEW_SUCCESS"]} ^5{E.Target.Name}^7");
                await E.Origin.Tell(statLine);
            }
        }
    }
}
