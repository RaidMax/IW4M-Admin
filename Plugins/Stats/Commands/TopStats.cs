using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

using SharedLibraryCore;
using SharedLibraryCore.Objects;
using SharedLibraryCore.Services;
using IW4MAdmin.Plugins.Stats.Models;
using SharedLibraryCore.Database;
namespace IW4MAdmin.Plugins.Stats.Commands
{
    class TopStats : Command
    {
        public TopStats() : base("topstats", "view the top 5 players on this server", "ts", Player.Permission.User, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            var statsSvc = new GenericRepository<EFClientStatistics>();
            int serverId = E.Owner.GetHashCode();

            using (var db = new DatabaseContext())
            {
                var thirtyDaysAgo = DateTime.UtcNow.AddMonths(-1);
                var topStats = await (from stats in db.Set<EFClientStatistics>()
                                      join client in db.Clients
                                      on stats.ClientId equals client.ClientId
                                      join alias in db.Aliases
                                      on client.CurrentAliasId equals alias.AliasId
                                      where stats.TimePlayed >= 3600
                                      where client.Level != Player.Permission.Banned
                                      where client.LastConnection >= thirtyDaysAgo
                                      orderby stats.Skill descending
                                      select new
                                      {
                                          alias.Name,
                                          stats.KDR,
                                          stats.Skill
                                      })
                                      .Take(5)
                                      .ToListAsync();


                if (!E.Message.IsBroadcastCommand())
                {
                    await E.Origin.Tell("^5--Top Players--");

                    foreach (var stat in topStats)
                        await E.Origin.Tell($"^3{stat.Name}^7 - ^5{stat.KDR} ^7KDR | ^5{stat.Skill} ^7SKILL");
                }
                else
                {
                    await E.Owner.Broadcast("^5--Top Players--");

                    foreach (var stat in topStats)
                        await E.Owner.Broadcast($"^3{stat.Name}^7 - ^5{stat.KDR} ^7KDR | ^5{stat.Skill} ^7SKILL");
                }
            }
        }
    }
}
