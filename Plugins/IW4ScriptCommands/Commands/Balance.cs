using SharedLibraryCore;
using SharedLibraryCore.Objects;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IW4ScriptCommands.Commands
{
    class Balance : Command
    {
        public Balance() : base("balance", "balance teams", "bal", Player.Permission.Trusted, false, null)
        {
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            List<string> teamAssignments = new List<string>();

            var clients = E.Owner.GetPlayersAsList().Select(c => new
            {
                Num = c.ClientNumber,
                Elo = IW4MAdmin.Plugins.Stats.Plugin.Manager.GetClientStats(c.ClientId, E.Owner.GetHashCode()).EloRating,
                CurrentTeam = IW4MAdmin.Plugins.Stats.Plugin.Manager.GetClientStats(c.ClientId, E.Owner.GetHashCode()).Team
            })
            .OrderByDescending(c => c.Elo)
            .ToList();

            int team = 0;
            for (int i = 0; i < clients.Count(); i++)
            {
                if (i == 0)
                {
                    team = 1;
                    continue;
                }
                if (i == 1)
                {
                    team = 2;
                    continue;
                }
                if (i == 2)
                {
                    team = 2;
                    continue;
                }
                if (i % 2 == 0)
                {
                    if (team == 1)
                        team = 2;
                    else
                        team = 1;
                }

                teamAssignments.Add($"{clients[i].Num},{team}");
            }

            string args = string.Join(",", teamAssignments);
            await E.Owner.SetDvarAsync("sv_iw4madmin_commandargs", args);
            await E.Owner.ExecuteCommandAsync("sv_iw4madmin_command balance");
            await E.Origin.Tell("Balance command sent");
        }
    }
}
