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
        public CViewStats() : base("stats", "view your stats", "xlrstats", Player.Permission.User, false, new CommandArgument[]
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
            if (E.Target?.ClientNumber < 0)
            {
                await E.Origin.Tell("The specified player must be ingame");
                return;
            }

            if (E.Origin.ClientNumber < 0 && E.Target == null)
            {
                await E.Origin.Tell("You must be ingame to view your stats");
                return;
            }

            String statLine;
            EFClientStatistics pStats;

            if (E.Data.Length > 0 && E.Target == null)
            {
                await E.Origin.Tell("Cannot find the player you specified");
                return;
            }

            var clientStats = new GenericRepository<EFClientStatistics>();
            int serverId = E.Owner.GetHashCode();

            if (E.Target != null)
            {
                pStats = clientStats.Find(c => c.ServerId == serverId && c.ClientId == E.Target.ClientId).First();
                statLine = String.Format("^5{0} ^7KILLS | ^5{1} ^7DEATHS | ^5{2} ^7KDR | ^5{3} ^7SKILL", pStats.Kills, pStats.Deaths, pStats.KDR, pStats.Skill);
            }

            else
            {
                pStats = pStats = clientStats.Find(c => c.ServerId == serverId && c.ClientId == E.Origin.ClientId).First();
                statLine = String.Format("^5{0} ^7KILLS | ^5{1} ^7DEATHS | ^5{2} ^7KDR | ^5{3} ^7SKILL", pStats.Kills, pStats.Deaths, pStats.KDR, pStats.Skill);
            }

            if (E.Message.IsBroadcastCommand())
            {
                string name = E.Target == null ? E.Origin.Name : E.Target.Name;
                await E.Owner.Broadcast($"Stats for ^5{name}^7");
                await E.Owner.Broadcast(statLine);
            }

            else
            {
                if (E.Target != null)
                    await E.Origin.Tell($"Stats for ^5{E.Target.Name}^7");
                await E.Origin.Tell(statLine);
            }
        }
    }
}
