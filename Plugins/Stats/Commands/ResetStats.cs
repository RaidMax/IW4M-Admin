using SharedLibraryCore;
using SharedLibraryCore.Objects;
using IW4MAdmin.Plugins.Stats.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IW4MAdmin.Plugins.Stats.Commands
{
    public class ResetStats : Command
    {
        public ResetStats() : base("resetstats", Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_RESET_DESC"], "rs", Player.Permission.User, false) { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Origin.ClientNumber >= 0)
            {
                var svc = new SharedLibraryCore.Services.GenericRepository<EFClientStatistics>();
                int serverId = E.Owner.GetHashCode();
                var stats = svc.Find(s => s.ClientId == E.Origin.ClientId && s.ServerId == serverId).First();

                stats.Deaths = 0;
                stats.Kills = 0;
                stats.SPM = 0.0;
                stats.Skill = 0.0;
                stats.TimePlayed = 0;

                // reset the cached version
                Plugin.Manager.ResetStats(E.Origin.ClientId, E.Owner.GetHashCode());

                // fixme: this doesn't work properly when another context exists
                await svc.SaveChangesAsync();
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_RESET_SUCCESS"]);
            }

            else
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_RESET_FAIL"]);
            }
        }
    }
}
