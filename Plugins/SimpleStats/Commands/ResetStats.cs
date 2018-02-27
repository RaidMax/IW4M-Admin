using SharedLibrary;
using SharedLibrary.Objects;
using StatsPlugin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin.Commands
{

    public class ResetStats : Command
    {
        public ResetStats() : base("resetstats", "reset your stats to factory-new", "rs", Player.Permission.User, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Origin.ClientNumber >= 0)
            {
                var svc = new SharedLibrary.Services.GenericRepository<EFClientStatistics>();
                int serverId = E.Owner.GetHashCode();
                var stats = svc.Find(s => s.ClientId == E.Origin.ClientId && s.ServerId == serverId).First();

                stats.Deaths = 0;
                stats.Kills = 0;
                stats.SPM = 0;
                stats.Skill = 0;

                // fixme: this doesn't work properly when another context exists
                await svc.SaveChangesAsync();
                await E.Origin.Tell("Your stats have been reset");
            }

            else
            {
                await E.Origin.Tell("You must be connected to a server to reset your stats");
            }
        }
    }
}
