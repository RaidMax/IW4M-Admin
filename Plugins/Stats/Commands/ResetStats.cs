using SharedLibraryCore;
using SharedLibraryCore.Objects;
using IW4MAdmin.Plugins.Stats.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharedLibraryCore.Database;
using Microsoft.EntityFrameworkCore;

namespace IW4MAdmin.Plugins.Stats.Commands
{
    public class ResetStats : Command
    {
        public ResetStats() : base("resetstats", Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_RESET_DESC"], "rs", Player.Permission.User, false) { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Origin.ClientNumber >= 0)
            {

                int serverId = E.Owner.GetHashCode();

                EFClientStatistics clientStats;
                using (var ctx = new DatabaseContext(disableTracking: true))
                {
                    clientStats = await ctx.Set<EFClientStatistics>()
                        .Where(s => s.ClientId == E.Origin.ClientId && s.ServerId == serverId)
                        .FirstAsync();

                    clientStats.Deaths = 0;
                    clientStats.Kills = 0;
                    clientStats.SPM = 0.0;
                    clientStats.Skill = 0.0;
                    clientStats.TimePlayed = 0;
                    // todo: make this more dynamic
                    clientStats.EloRating = 200.0;

                    // reset the cached version
                    Plugin.Manager.ResetStats(E.Origin.ClientId, E.Owner.GetHashCode());

                    // fixme: this doesn't work properly when another context exists
                    await ctx.SaveChangesAsync();
                }
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_RESET_SUCCESS"]);
            }

            else
            {
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_RESET_FAIL"]);
            }
        }
    }
}
