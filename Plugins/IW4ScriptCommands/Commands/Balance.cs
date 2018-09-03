//using SharedLibraryCore;
//using SharedLibraryCore.Objects;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace IW4ScriptCommands.Commands
//{
//    class Balance : Command
//    {
//        private class TeamAssignment
//        {
//            public IW4MAdmin.Plugins.Stats.IW4Info.Team CurrentTeam { get; set; }
//            public int Num { get; set; }
//            public IW4MAdmin.Plugins.Stats.Models.EFClientStatistics Stats { get; set; }
//        }
//        public Balance() : base("balance", "balance teams", "bal", Player.Permission.Trusted, false, null)
//        {
//        }

//        public override async Task ExecuteAsync(GameEvent E)
//        {
//            string teamsString = (await E.Owner.GetDvarAsync<string>("sv_iw4madmin_teams")).Value;

//            var scriptClientTeams = teamsString.Split(';', StringSplitOptions.RemoveEmptyEntries)
//                .Select(c => c.Split(','))
//                .Select(c => new TeamAssignment()
//                {
//                    CurrentTeam = (IW4MAdmin.Plugins.Stats.IW4Info.Team)Enum.Parse(typeof(IW4MAdmin.Plugins.Stats.IW4Info.Team), c[1]),
//                    Num = E.Owner.Players.FirstOrDefault(p => p?.NetworkId == c[0].ConvertLong())?.ClientNumber ?? -1,
//                    Stats = IW4MAdmin.Plugins.Stats.Plugin.Manager.GetClientStats(E.Owner.Players.FirstOrDefault(p => p?.NetworkId == c[0].ConvertLong()).ClientId, E.Owner.GetHashCode())
//                })
//                .ToList();

//            // at least one team is full so we can't balance
//            if (scriptClientTeams.Count(ct => ct.CurrentTeam == IW4MAdmin.Plugins.Stats.IW4Info.Team.Axis) >= Math.Floor(E.Owner.MaxClients / 2.0)
//                || scriptClientTeams.Count(ct => ct.CurrentTeam == IW4MAdmin.Plugins.Stats.IW4Info.Team.Allies) >= Math.Floor(E.Owner.MaxClients / 2.0))
//            {
//                await E.Origin?.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_BALANCE_FAIL"]);
//                return;
//            }

//            List<string> teamAssignments = new List<string>();

//            var activeClients = E.Owner.GetPlayersAsList().Select(c => new TeamAssignment()
//            {
//                Num = c.ClientNumber,
//                Stats = IW4MAdmin.Plugins.Stats.Plugin.Manager.GetClientStats(c.ClientId, E.Owner.GetHashCode()),
//                CurrentTeam = IW4MAdmin.Plugins.Stats.Plugin.Manager.GetClientStats(c.ClientId, E.Owner.GetHashCode()).Team
//            })
//            .Where(c => scriptClientTeams.FirstOrDefault(sc => sc.Num == c.Num)?.CurrentTeam != IW4MAdmin.Plugins.Stats.IW4Info.Team.Spectator)
//            .Where(c => c.CurrentTeam != scriptClientTeams.FirstOrDefault(p => p.Num == c.Num)?.CurrentTeam)
//            .OrderByDescending(c => c.Stats.Performance)
//            .ToList();

//            var alliesTeam = scriptClientTeams
//                .Where(c => c.CurrentTeam == IW4MAdmin.Plugins.Stats.IW4Info.Team.Allies)
//                .Where(c => activeClients.Count(t => t.Num == c.Num) == 0)
//                .ToList();

//            var axisTeam = scriptClientTeams
//                .Where(c => c.CurrentTeam == IW4MAdmin.Plugins.Stats.IW4Info.Team.Axis)
//                .Where(c => activeClients.Count(t => t.Num == c.Num) == 0)
//                .ToList();

//            while (activeClients.Count() > 0)
//            {
//                int teamSizeDifference = alliesTeam.Count - axisTeam.Count;
//                double performanceDisparity = alliesTeam.Count > 0 ? alliesTeam.Average(t => t.Stats.Performance) : 0 -
//                    axisTeam.Count > 0 ? axisTeam.Average(t => t.Stats.Performance) : 0;

//                if (teamSizeDifference == 0)
//                {
//                    if (performanceDisparity == 0)
//                    {
//                        alliesTeam.Add(activeClients.First());
//                        activeClients.RemoveAt(0);
//                    }
//                    else
//                    {
//                        if (performanceDisparity > 0)
//                        {
//                            axisTeam.Add(activeClients.First());
//                            activeClients.RemoveAt(0);
//                        }
//                        else
//                        {
//                            alliesTeam.Add(activeClients.First());
//                            activeClients.RemoveAt(0);
//                        }
//                    }
//                }
//                else if (teamSizeDifference > 0)
//                {
//                    if (performanceDisparity > 0)
//                    {
//                        axisTeam.Add(activeClients.First());
//                        activeClients.RemoveAt(0);
//                    }

//                    else
//                    {
//                        axisTeam.Add(activeClients.Last());
//                        activeClients.RemoveAt(activeClients.Count - 1);
//                    }
//                }
//                else
//                {
//                    if (performanceDisparity > 0)
//                    {
//                        alliesTeam.Add(activeClients.First());
//                        activeClients.RemoveAt(0);
//                    }

//                    else
//                    {
//                        alliesTeam.Add(activeClients.Last());
//                        activeClients.RemoveAt(activeClients.Count - 1);
//                    }
//                }
//            }

//            alliesTeam = alliesTeam.OrderByDescending(t => t.Stats.Performance)
//                .ToList();

//            axisTeam = axisTeam.OrderByDescending(t => t.Stats.Performance)
//                .ToList();

//            while (Math.Abs(alliesTeam.Count - axisTeam.Count) > 1)
//            {
//                int teamSizeDifference = alliesTeam.Count - axisTeam.Count;
//                double performanceDisparity = alliesTeam.Count > 0 ? alliesTeam.Average(t => t.Stats.Performance) : 0 -
//                    axisTeam.Count > 0 ? axisTeam.Average(t => t.Stats.Performance) : 0;

//                if (teamSizeDifference > 0)
//                {
//                    if (performanceDisparity > 0)
//                    {
//                        axisTeam.Add(alliesTeam.First());
//                        alliesTeam.RemoveAt(0);
//                    }

//                    else
//                    {
//                        axisTeam.Add(alliesTeam.Last());
//                        alliesTeam.RemoveAt(axisTeam.Count - 1);
//                    }
//                }

//                else
//                {
//                    if (performanceDisparity > 0)
//                    {
//                        alliesTeam.Add(axisTeam.Last());
//                        axisTeam.RemoveAt(axisTeam.Count - 1);
//                    }

//                    else
//                    {
//                        alliesTeam.Add(axisTeam.First());
//                        axisTeam.RemoveAt(0);
//                    }
//                }
//            }

//            foreach (var assignment in alliesTeam)
//            {
//                teamAssignments.Add($"{assignment.Num},2");
//                assignment.Stats.Team = IW4MAdmin.Plugins.Stats.IW4Info.Team.Allies;
//            }
//            foreach (var assignment in axisTeam)
//            {
//                teamAssignments.Add($"{assignment.Num},3");
//                assignment.Stats.Team = IW4MAdmin.Plugins.Stats.IW4Info.Team.Axis;
//            }

//            if (alliesTeam.Count(ac => scriptClientTeams.First(sc => sc.Num == ac.Num).CurrentTeam != ac.CurrentTeam) == 0 &&
//                axisTeam.Count(ac => scriptClientTeams.First(sc => sc.Num == ac.Num).CurrentTeam != ac.CurrentTeam) == 0)
//            {
//                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_BALANCE_FAIL_BALANCED"]);
//                return;
//            }

//            if (E.Origin?.Level > Player.Permission.Administrator)
//            {
//                await E.Origin.Tell($"Allies Elo: {(alliesTeam.Count > 0 ? alliesTeam.Average(t => t.Stats.Performance) : 0)}");
//                await E.Origin.Tell($"Axis Elo: {(axisTeam.Count > 0 ? axisTeam.Average(t => t.Stats.Performance) : 0)}");
//            }

//            string args = string.Join(",", teamAssignments);
//            await E.Owner.ExecuteCommandAsync($"sv_iw4madmin_command \"balance:{args}\"");
//            await E.Origin.Tell("Balance command sent");
//        }
//    }
//}
