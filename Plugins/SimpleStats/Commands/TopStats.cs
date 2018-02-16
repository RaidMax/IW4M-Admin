using SharedLibrary;
using SharedLibrary.Objects;
using SharedLibrary.Services;
using StatsPlugin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin.Commands
{
    class TopStats : Command
    {
        public TopStats() : base("topstats", "view the top 5 players on this server", "ts", Player.Permission.User, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            var statsSvc = new GenericRepository<EFClientStatistics>();
            int serverId = E.Origin.GetHashCode();
            var iqStats = statsSvc.GetQuery(cs => cs.ServerId == serverId);

            var topStats = iqStats.Where(cs => cs.Skill > 100)
                .OrderByDescending(cs => cs.Skill)
                .Take(5)
                .ToList();

            if (!E.Message.IsBroadcastCommand())
            {
                await E.Origin.Tell("^5--Top Players--");

                foreach (var stat in topStats)
                    await E.Origin.Tell($"^3{stat.Client.Name}^7 - ^5{stat.KDR} ^7KDR | ^5{stat.Skill} ^7SKILL");
            }
            else
            {
                await E.Owner.Broadcast("^5--Top Players--");

                foreach (var stat in topStats)
                    await E.Owner.Broadcast($"^3{stat.Client.Name}^7 - ^5{stat.KDR} ^7KDR | ^5{stat.Skill} ^7SKILL");
            }
        }
    }
}
