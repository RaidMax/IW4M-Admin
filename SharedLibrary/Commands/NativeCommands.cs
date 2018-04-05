using SharedLibrary.Database;
using SharedLibrary.Database.Models;
using SharedLibrary.Exceptions;
using SharedLibrary.Objects;
using SharedLibrary.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Commands
{
    public class CQuit : Command
    {
        public CQuit() :
            base("quit", "quit IW4MAdmin", "q", Player.Permission.Owner, false)
        { }

        public override Task ExecuteAsync(Event E)
        {
            return Task.Run(() => { E.Owner.Manager.Stop(); });
        }
    }

    public class COwner : Command
    {
        public COwner() :
            base("owner", "claim ownership of the server", "o", Player.Permission.User, false)
        { }

        public override async Task ExecuteAsync(Event E)
        {
            if ((await (E.Owner.Manager.GetClientService() as Services.ClientService).GetOwners()).Count == 0)
            {
                E.Origin.Level = Player.Permission.Owner;
                await E.Origin.Tell("Congratulations, you have claimed ownership of this server!");
                await E.Owner.Manager.GetClientService().Update(E.Origin);
            }
            else
                await E.Origin.Tell("This server already has an owner!");
        }
    }

    public class CWarn : Command
    {
        public CWarn() :
            base("warn", "warn player for infringing rules", "w", Player.Permission.Trusted, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = "player",
                        Required = true
                    },
                    new CommandArgument()
                    {
                        Name = "reason",
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Origin.Level <= E.Target.Level)
                await E.Origin.Tell($"You do not have the required privileges to warn {E.Target.Name}");
            else
                await E.Target.Warn(E.Data, E.Origin);
        }
    }

    public class CWarnClear : Command
    {
        public CWarnClear() :
            base("warnclear", "remove all warning for a player", "wc", Player.Permission.Trusted, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = "player",
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            E.Target.Warnings = 0;
            String Message = String.Format("All warning cleared for {0}", E.Target.Name);
            await E.Owner.Broadcast(Message);
        }
    }

    public class CKick : Command
    {
        public CKick() :
            base("kick", "kick a player by name", "k", Player.Permission.Trusted, true, new CommandArgument[]
            {
                new CommandArgument()
                {
                    Name = "player",
                    Required = true
                },
                new CommandArgument()
                {
                Name = "reason",
                Required = true
                }
            })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Origin.Level > E.Target.Level)
            {
                await E.Owner.ExecuteEvent(new Event(Event.GType.Kick, E.Data, E.Origin, E.Target, E.Owner));
                await E.Target.Kick(E.Data, E.Origin);
                await E.Origin.Tell($"^5{E.Target} ^7has been kicked");
            }
            else
                await E.Origin.Tell($"You do not have the required privileges to kick {E.Target.Name}");
        }
    }

    public class CSay : Command
    {
        public CSay() :
            base("say", "broadcast message to all players", "s", Player.Permission.Moderator, false, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = "message",
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            await E.Owner.Broadcast($"^:{E.Origin.Name} - ^6{E.Data}^7");
        }
    }

    public class CTempBan : Command
    {
        public CTempBan() :
            base("tempban", "temporarily ban a player for specified time (defaults to 1 hour)", "tb", Player.Permission.Moderator, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = "player",
                        Required = true
                    },
                    new CommandArgument()
                    {
                        Name = "duration (m|h|d|w|y)",
                        Required = true,
                    },
                    new CommandArgument()
                    {
                        Name = "reason",
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            String Message = Utilities.RemoveWords(E.Data, 1).Trim();
            var length = E.Data.Split(' ')[0].ToLower().ParseTimespan();
            if (length.TotalHours >= 1 && length.TotalHours < 2)
                Message = E.Data.Replace("1h", "").Replace("1H", "");

            if (E.Origin.Level > E.Target.Level)
            {
                await E.Target.TempBan(Message, length, E.Origin);
                await E.Origin.Tell($"^5{E.Target} ^7has been temporarily banned for ^5{length.TimeSpanText()}");
            }
            else
                await E.Origin.Tell("You cannot temp ban " + E.Target.Name);
        }
    }

    public class CBan : Command
    {
        public CBan() :
            base("ban", "permanently ban a player from the server", "b", Player.Permission.SeniorAdmin, true, new CommandArgument[]
            {
                new CommandArgument()
                {
                    Name = "player",
                    Required = true
                },
                new CommandArgument()
                {
                    Name = "reason",
                    Required = true
                }
            })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Origin.Level > E.Target.Level)
            {
                await E.Target.Ban(E.Data, E.Origin);
                await E.Origin.Tell($"^5{E.Target} ^7has been permanently banned");
            }
            else
                await E.Origin.Tell("You cannot ban " + E.Target.Name);
        }
    }

    public class CUnban : Command
    {
        public CUnban() :
            base("unban", "unban player by database id", "ub", Player.Permission.SeniorAdmin, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = "databaseID",
                        Required = true,
                    },
                    new CommandArgument()
                    {
                        Name = "reason",
                        Required  = true
                    }
                })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            var penalties = await E.Owner.Manager.GetPenaltyService().GetActivePenaltiesAsync(E.Target.AliasLinkId);
            if (penalties.Where(p => p.Type == Penalty.PenaltyType.Ban).FirstOrDefault() != null)
            {
                await E.Owner.Unban(E.Data, E.Target, E.Origin);
                await E.Origin.Tell($"Successfully unbanned {E.Target}");
            }
            else
            {
                await E.Origin.Tell($"{E.Target} is not banned");
            }
        }
    }

    public class CWhoAmI : Command
    {
        public CWhoAmI() :
            base("whoami", "give information about yourself.", "who", Player.Permission.User, false)
        { }

        public override async Task ExecuteAsync(Event E)
        {
            String You = String.Format("{0} [^3#{1}^7] {2} [^3@{3}^7] [{4}^7] IP: {5}", E.Origin.Name, E.Origin.ClientNumber, E.Origin.NetworkId, E.Origin.ClientId, Utilities.ConvertLevelToColor(E.Origin.Level), E.Origin.IPAddressString);
            await E.Origin.Tell(You);
        }
    }

    public class CList : Command
    {
        public CList() :
            base("list", "list active clients", "l", Player.Permission.Moderator, false)
        { }

        public override async Task ExecuteAsync(Event E)
        {
            StringBuilder playerList = new StringBuilder();
            int count = 0;
            for (int i = 0; i < E.Owner.Players.Count; i++)
            {
                var P = E.Owner.Players[i];

                if (P == null)
                    continue;

                if (P.Masked)
                    playerList.AppendFormat("[^3{0}^7]{3}[^3{1}^7] {2}", Utilities.ConvertLevelToColor(Player.Permission.User), P.ClientNumber, P.Name, Utilities.GetSpaces(Player.Permission.SeniorAdmin.ToString().Length - Player.Permission.User.ToString().Length));
                else
                    playerList.AppendFormat("[^3{0}^7]{3}[^3{1}^7] {2}", Utilities.ConvertLevelToColor(P.Level), P.ClientNumber, P.Name, Utilities.GetSpaces(Player.Permission.SeniorAdmin.ToString().Length - P.Level.ToString().Length));

                if (count == 2 || E.Owner.GetPlayersAsList().Count == 1)
                {
                    await E.Origin.Tell(playerList.ToString());
                    count = 0;
                    playerList = new StringBuilder();
                    continue;
                }

                count++;
            }
        }
    }

    public class CHelp : Command
    {
        public CHelp() :
            base("help", "list all available commands", "h", Player.Permission.User, false, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = "command",
                        Required = false
                    }
                })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            String cmd = E.Data.Trim();

            if (cmd.Length > 2)
            {
                bool found = false;
                foreach (Command C in E.Owner.Manager.GetCommands())
                {
                    if (C.Name == cmd.ToLower())
                    {
                        await E.Origin.Tell("[^3" + C.Name + "^7] " + C.Description);
                        await E.Origin.Tell(C.Syntax);
                        found = true;
                    }
                }

                if (!found)
                    await E.Origin.Tell("Could not find that command");
            }

            else
            {
                int count = 0;
                StringBuilder helpResponse = new StringBuilder();
                var CommandList = E.Owner.Manager.GetCommands();

                foreach (Command C in CommandList)
                {
                    if (E.Origin.Level >= C.Permission)
                    {
                        helpResponse.Append(" [^3" + C.Name + "^7] ");
                        if (count >= 4)
                        {
                            if (E.Message[0] == '@')
                                await E.Owner.Broadcast(helpResponse.ToString());
                            else
                                await E.Origin.Tell(helpResponse.ToString());
                            helpResponse = new StringBuilder();
                            count = 0;
                        }
                        count++;
                    }
                }
                await E.Origin.Tell(helpResponse.ToString());
                await E.Origin.Tell("Type !help <cmd> to get command usage syntax");
            }
        }
    }

    public class CFastRestart : Command
    {
        public CFastRestart() :
            base("fastrestart", "fast restart current map", "fr", Player.Permission.Moderator, false)
        { }

        public override async Task ExecuteAsync(Event E)
        {
            await E.Owner.ExecuteCommandAsync("fast_restart");

            if (!E.Origin.Masked)
                await E.Owner.Broadcast($"^5{E.Origin.Name} ^7fast restarted the server");
            else
                await E.Owner.Broadcast($"The server has been fast restarted");
        }
    }

    public class CMapRotate : Command
    {
        public CMapRotate() :
            base("maprotate", "cycle to the next map in rotation", "mr", Player.Permission.Administrator, false)
        { }

        public override async Task ExecuteAsync(Event E)
        {
            if (!E.Origin.Masked)
                await E.Owner.Broadcast($"Map rotating in ^55 ^7seconds [^5{E.Origin.Name}^7]");
            else
                await E.Owner.Broadcast($"Map rotating in ^55 ^7seconds [^5Masked Admin^7]");
            Task.Delay(5000).Wait();
            await E.Owner.ExecuteCommandAsync("map_rotate");
        }
    }

    public class CSetLevel : Command
    {
        public CSetLevel() :
            base("setlevel", "set player to specified administration level", "sl", Player.Permission.Moderator, true, new CommandArgument[]
                {
                     new CommandArgument()
                     {
                         Name = "player",
                         Required = true
                     },
                     new CommandArgument()
                     {
                         Name = "level",
                         Required = true
                     }
                })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Target == E.Origin)
            {
                await E.Origin.Tell("You cannot change your own level");
                return;
            }

            Player.Permission newPerm = Utilities.MatchPermission(E.Data);

            if (newPerm == Player.Permission.Owner &&
                !E.Owner.Manager.GetApplicationSettings().Configuration().EnableMultipleOwners)
            {
                await E.Origin.Tell("There can only be 1 owner. Modify your settings if multiple owners are required");
                return;
            }

            if (E.Origin.Level < Player.Permission.Owner &&
                !E.Owner.Manager.GetApplicationSettings().Configuration().EnableSteppedHierarchy)
            {
                await E.Origin.Tell($"This server does not allow you to promote ^5{E.Target.Name}");
                return;
            }

            if (newPerm >= E.Origin.Level)
            {
                if (E.Origin.Level < Player.Permission.Owner)
                {
                    await E.Origin.Tell($"You can only promote ^5{E.Target.Name} ^7to ^5{(E.Origin.Level - 1)} ^7or lower privilege");
                    return;
                }
            }

            if (newPerm > Player.Permission.Banned)
            {
                var ActiveClient = E.Owner.Manager.GetActiveClients()
                    .FirstOrDefault(p => p.NetworkId == E.Target.NetworkId);

                if (ActiveClient != null)
                {
                    ActiveClient.Level = newPerm;
                    await ActiveClient.Tell("Congratulations! You have been promoted to ^3" + newPerm);
                }

                else
                {
                    E.Target.Level = newPerm;
                    await E.Owner.Manager.GetClientService().Update(E.Target);
                }

                try
                {
                    E.Owner.Manager.GetPrivilegedClients().Add(E.Target.ClientId, E.Target);
                }

                catch (Exception)
                {
                    E.Owner.Manager.GetPrivilegedClients()[E.Target.ClientId] = E.Target;
                }

                await E.Origin.Tell($"{E.Target.Name} was successfully promoted");
            }

            else
                await E.Origin.Tell("Invalid group specified");
        }
    }

    public class CUsage : Command
    {
        public CUsage() :
            base("usage", "get current application memory usage", "us", Player.Permission.Moderator, false)
        { }

        public override async Task ExecuteAsync(Event E)
        {
            await E.Origin.Tell("IW4M Admin is using " + Math.Round(((System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 2048f) / 1200f), 1) + "MB");
        }
    }

    public class CUptime : Command
    {
        public CUptime() :
            base("uptime", "get current application running time", "up", Player.Permission.Moderator, false)
        { }

        public override async Task ExecuteAsync(Event E)
        {
            TimeSpan uptime = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
            await E.Origin.Tell(String.Format("IW4M Admin has been up for {0} days, {1} hours, and {2} minutes", uptime.Days, uptime.Hours, uptime.Minutes));
        }
    }

    public class CListAdmins : Command
    {
        public CListAdmins() :
            base("admins", "list currently connected admins", "a", Player.Permission.User, false)
        { }

        public override async Task ExecuteAsync(Event E)
        {
            int numOnline = 0;
            for (int i = 0; i < E.Owner.Players.Count; i++)
            {
                var P = E.Owner.Players[i];
                if (P != null && P.Level > Player.Permission.Flagged && !P.Masked)
                {
                    numOnline++;
                    if (E.Message[0] == '@')
                        await E.Owner.Broadcast(String.Format("[^3{0}^7] {1}", Utilities.ConvertLevelToColor(P.Level), P.Name));
                    else
                        await E.Origin.Tell(String.Format("[^3{0}^7] {1}", Utilities.ConvertLevelToColor(P.Level), P.Name));
                }
            }

            if (numOnline == 0)
                await E.Origin.Tell("No visible administrators online");
        }
    }

    public class CLoadMap : Command
    {
        public CLoadMap() :
            base("map", "change to specified map", "m", Player.Permission.Administrator, false, new CommandArgument[]
            {
                 new CommandArgument()
                 {
                     Name = "map",
                     Required = true
                 }
            })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            string newMap = E.Data.Trim().ToLower();
            foreach (Map m in E.Owner.Maps)
            {
                if (m.Name.ToLower() == newMap || m.Alias.ToLower() == newMap)
                {
                    await E.Owner.Broadcast($"Changing to map ^5{m.Alias}");
                    Task.Delay(5000).Wait();
                    await E.Owner.LoadMap(m.Name);
                    return;
                }
            }

            await E.Owner.Broadcast($"Attempting to change to unknown map ^5{newMap}");
            Task.Delay(5000).Wait();
            await E.Owner.LoadMap(newMap);
        }
    }

    public class CFindPlayer : Command
    {
        public CFindPlayer() :
            base("find", "find player in database", "f", Player.Permission.Administrator, false, new CommandArgument[]
            {
                new CommandArgument()
                {
                    Name = "player",
                    Required = true
                }
            })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            var db_players = (await (E.Owner.Manager.GetClientService() as ClientService)
                .GetClientByName(E.Data))
                .OrderByDescending(p => p.LastConnection)
                .ToList();

            if (db_players.Count == 0)
            {
                await E.Origin.Tell("No players found");
                return;
            }

            foreach (var P in db_players)
            {
                // they're not going by another alias
                string msg = P.Name.ToLower().Contains(E.Data.ToLower()) ?
                    $"[^3{P.Name}^7] [^3@{P.ClientId}^7] - [{ Utilities.ConvertLevelToColor(P.Level)}^7] - {P.IPAddressString} | last seen {Utilities.GetTimePassed(P.LastConnection)}" :
                    $"({P.AliasLink.Children.First(a => a.Name.ToLower().Contains(E.Data.ToLower())).Name})->[^3{P.Name}^7] [^3@{P.ClientId}^7] - [{ Utilities.ConvertLevelToColor(P.Level)}^7] - {P.IPAddressString} | last seen {Utilities.GetTimePassed(P.LastConnection)}";
                await E.Origin.Tell(msg);
            }
        }
    }

    public class CListRules : Command
    {
        public CListRules() :
            base("rules", "list server rules", "r", Player.Permission.User, false)
        { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Owner.Manager.GetApplicationSettings().Configuration().GlobalRules?.Count < 1 &&
                E.Owner.ServerConfig.Rules?.Count < 1)
            {
                if (E.Message.IsBroadcastCommand())
                    await E.Owner.Broadcast("The server owner has not set any rules");
                else
                    await E.Origin.Tell("The server owner has not set any rules");
            }

            else
            {
                var rules = new List<string>();
                rules.AddRange(E.Owner.Manager.GetApplicationSettings().Configuration().GlobalRules);
                if (E.Owner.ServerConfig.Rules != null)
                    rules.AddRange(E.Owner.ServerConfig.Rules);

                foreach (string r in rules)
                {
                    if (E.Message.IsBroadcastCommand())
                        await E.Owner.Broadcast($"- {r}");
                    else
                        await E.Origin.Tell($"- {r}");
                }
            }
        }
    }

    public class CPrivateMessage : Command
    {
        public CPrivateMessage() :
            base("privatemessage", "send message to other player", "pm", Player.Permission.User, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = "player",
                        Required = true
                    },
                    new CommandArgument()
                    {
                        Name = "message",
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            await E.Target.Tell($"^1{E.Origin.Name} ^3[PM]^7 - {E.Data}");
            await E.Origin.Tell($"To ^3{E.Target.Name} ^7-> {E.Data}");
        }
    }

    public class CFlag : Command
    {
        public CFlag() :
            base("flag", "flag a suspicious player and announce to admins on join", "fp", Player.Permission.Moderator, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = "player",
                        Required = true
                    },
                    new CommandArgument()
                    {
                        Name = "reason",
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            // todo: move unflag to seperate command
            if (E.Target.Level >= E.Origin.Level)
            {
                await E.Origin.Tell($"You cannot flag {E.Target.Name}");
                return;
            }

            if (E.Target.Level == Player.Permission.Flagged)
            {
                E.Target.Level = Player.Permission.User;
                await E.Owner.Manager.GetClientService().Update(E.Target);
                await E.Origin.Tell($"You have ^5unflagged ^7{E.Target.Name}");
            }

            else
            {
                E.Target.Level = Player.Permission.Flagged;

                Penalty newPenalty = new Penalty()
                {
                    Type = Penalty.PenaltyType.Flag,
                    Expires = DateTime.UtcNow,
                    Offender = E.Target,
                    Offense = E.Data,
                    Punisher = E.Origin,
                    Active = true,
                    When = DateTime.UtcNow,
                    Link = E.Target.AliasLink
                };

                await E.Owner.Manager.GetPenaltyService().Create(newPenalty);
                await E.Owner.ExecuteEvent(new Event(Event.GType.Flag, E.Data, E.Origin, E.Target, E.Owner));
                await E.Origin.Tell($"You have flagged ^5{E.Target.Name}");
            }

        }
    }

    public class CReport : Command
    {
        public CReport() :
            base("report", "report a player for suspicious behavior", "rep", Player.Permission.User, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = "player",
                        Required = true
                    },
                    new CommandArgument()
                    {
                        Name = "reason",
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Data.ToLower().Contains("camp"))
            {
                await E.Origin.Tell("You cannot report a player for camping");
                return;
            }

            if (E.Owner.Reports.Find(x => (x.Origin == E.Origin && x.Target.NetworkId == E.Target.NetworkId)) != null)
            {
                await E.Origin.Tell("You have already reported this player");
                return;
            }

            if (E.Target == E.Origin)
            {
                await E.Origin.Tell("You cannot report yourself");
                return;
            }

            if (E.Target.Level > E.Origin.Level)
            {
                await E.Origin.Tell($"You cannot report {E.Target.Name}");
                return;
            }

            E.Owner.Reports.Add(new Report(E.Target, E.Origin, E.Data));

            await E.Origin.Tell($"Thank you for your report, an administrator has been notified");
            await E.Owner.ExecuteEvent(new Event(Event.GType.Report, E.Data, E.Origin, E.Target, E.Owner));
            await E.Owner.ToAdmins(String.Format("^5{0}^7->^1{1}^7: {2}", E.Origin.Name, E.Target.Name, E.Data));
        }
    }

    public class CListReports : Command
    {
        public CListReports() :
            base("reports", "get or clear recent reports", "reps", Player.Permission.Moderator, false, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = "clear",
                        Required = false
                    }
                })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Data != null && E.Data.ToLower().Contains("clear"))
            {
                E.Owner.Reports = new List<Report>();
                await E.Origin.Tell("Reports successfully cleared!");
                return;
            }

            if (E.Owner.Reports.Count < 1)
            {
                await E.Origin.Tell("No players reported yet");
                return;
            }

            foreach (Report R in E.Owner.Reports)
                await E.Origin.Tell(String.Format("^5{0}^7->^1{1}^7: {2}", R.Origin.Name, R.Target.Name, R.Reason));
        }
    }

    public class CMask : Command
    {
        public CMask() :
            base("mask", "hide your presence as an administrator", "hide", Player.Permission.Moderator, false)
        { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Origin.Masked)
            {
                E.Origin.Masked = false;
                await E.Origin.Tell("You are now unmasked");
            }
            else
            {
                E.Origin.Masked = true;
                await E.Origin.Tell("You are now masked");
            }

            await E.Owner.Manager.GetClientService().Update(E.Origin);
        }
    }

    public class CListBanInfo : Command
    {
        public CListBanInfo() :
            base("baninfo", "get information about a ban for a player", "bi", Player.Permission.Moderator, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = "player",
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            var B = await E.Owner.Manager.GetPenaltyService().GetClientPenaltiesAsync(E.Target.ClientId);

            var penalty = B.FirstOrDefault(b => b.Type > Penalty.PenaltyType.Kick && b.Expires > DateTime.UtcNow);

            if (penalty == null)
            {
                await E.Origin.Tell("No active ban was found for that player");
                return;
            }

            await E.Origin.Tell(String.Format("^1{0} ^7was banned by ^5{1} ^7for: {2} {3}", E.Target.Name, penalty.Punisher.Name, penalty.Offense, penalty.Type == Penalty.PenaltyType.TempBan ? $"({(penalty.Expires - DateTime.UtcNow).TimeSpanText()} remaining)" : ""));
        }
    }

    public class CListAlias : Command
    {
        public CListAlias() :
            base("alias", "get past aliases and ips of a player", "known", Player.Permission.Moderator, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = "player",
                        Required = true,
                    }
                })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            StringBuilder message = new StringBuilder();
            var names = new List<string>(E.Target.AliasLink.Children.Select(a => a.Name));
            var IPs = new List<string>(E.Target.AliasLink.Children.Select(a => a.IPAddress.ConvertIPtoString()).Distinct());

            await E.Target.Tell($"[^3{E.Target}^7]");

            message.Append("Aliases: ");
            message.Append(String.Join(" | ", names));
            await E.Origin.Tell(message.ToString());

            message.Clear();
            message.Append("IPs: ");
            message.Append(String.Join(" | ", IPs));
            await E.Origin.Tell(message.ToString());
        }
    }

    public class CExecuteRCON : Command
    {
        public CExecuteRCON() :
            base("rcon", "send rcon command to server", "rcon", Player.Permission.Owner, false, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = "command",
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            var Response = await E.Owner.ExecuteCommandAsync(E.Data.Trim());
            foreach (string S in Response)
                await E.Origin.Tell(S.StripColors());
            if (Response.Length == 0)
                await E.Origin.Tell("Successfully sent RCON command!");
        }
    }

    public class CPlugins : Command
    {
        public CPlugins() :
            base("plugins", "view all loaded plugins", "p", Player.Permission.Administrator, false)
        { }

        public override async Task ExecuteAsync(Event E)
        {
            await E.Origin.Tell("^5Loaded Plugins:");
            foreach (var P in Plugins.PluginImporter.ActivePlugins)
            {
                await E.Origin.Tell(String.Format("^3{0} ^7[v^3{1}^7] by ^5{2}^7", P.Name, P.Version, P.Author));
            }
        }
    }

    public class CIP : Command
    {
        public CIP() :
            base("getexternalip", "view your external IP address", "ip", Player.Permission.User, false)
        { }

        public override async Task ExecuteAsync(Event E)
        {
            await E.Origin.Tell($"Your external IP is ^5{E.Origin.IPAddressString}");
        }
    }

    public class CPruneAdmins : Command
    {
        public CPruneAdmins() : base("prune", "demote any admins that have not connected recently (defaults to 30 days)", "p", Player.Permission.Owner, false, new CommandArgument[]
        {
            new CommandArgument()
            {
                Name = "inactive days",
                Required = false
            }
        })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            int inactiveDays = 30;

            try
            {
                if (E.Data.Length > 0)
                {
                    inactiveDays = Int32.Parse(E.Data);
                    if (inactiveDays < 1)
                        throw new FormatException();
                }
            }

            catch (FormatException)
            {
                await E.Origin.Tell("Invalid number of inactive days");
                return;
            }

            List<EFClient> inactiveUsers = null;

            // update user roles
            using (var context = new DatabaseContext())
            {
                var lastActive = DateTime.UtcNow.AddDays(-inactiveDays);
                inactiveUsers = await context.Clients
                    .Where(c => c.Level > Player.Permission.Flagged && c.Level <= Player.Permission.Moderator)
                    .Where(c => c.LastConnection < lastActive)
                    .ToListAsync();
                inactiveUsers.ForEach(c => c.Level = Player.Permission.User);
                await context.SaveChangesAsync();
            }
            await E.Origin.Tell($"Pruned {inactiveUsers.Count} inactive privileged users");

        }
    }

    public class CSetPassword : Command
    {
        public CSetPassword() : base("setpassword", "set your authentication password", "sp", Player.Permission.Moderator, false, new CommandArgument[]
            {
                new CommandArgument()
                {
                    Name = "password",
                    Required = true
                }
            })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Data.Length < 5)
            {
                await E.Origin.Tell("Your password must be atleast 5 characters long");
                return;
            }

            string[] hashedPassword = Helpers.Hashing.Hash(E.Data);

            E.Origin.Password = hashedPassword[0];
            E.Origin.PasswordSalt = hashedPassword[1];

            // update the password for the client in privileged
            E.Owner.Manager.GetPrivilegedClients()[E.Origin.ClientId] = E.Origin;

            await E.Owner.Manager.GetClientService().Update(E.Origin);
            await E.Origin.Tell("Your password has been set successfully");
        }
    }

    public class CKillServer : Command
    {
        public CKillServer() : base("killserver", "kill the game server", "kill", Player.Permission.Administrator, false)
        {
        }

        public override async Task ExecuteAsync(Event E)
        {
            var gameserverProcesses = System.Diagnostics.Process.GetProcessesByName("iw4x");
            var currentProcess = gameserverProcesses.FirstOrDefault(g => g.MainWindowTitle.Contains(E.Owner.Hostname));

            if (currentProcess == null)
            {
                await E.Origin.Tell("Could not find running/stalled instance of IW4x");
            }

            else
            {
                // attempt to kill it natively
                try
                {
                    if (!E.Owner.Throttled)
                    {
#if !DEBUG
                        await E.Owner.ExecuteCommandAsync("quit");
#endif
                    }
                }

                catch (NetworkException)
                {
                    await E.Origin.Tell("Unable to cleanly shutdown server, forcing");
                }

                if (!currentProcess.HasExited)
                {
                    try
                    {
                        currentProcess.Kill();
                        await E.Origin.Tell("Successfully killed server process");
                    }
                    catch (Exception e)
                    {
                        await E.Origin.Tell("Could not kill server process");
                        E.Owner.Logger.WriteDebug("Unable to kill process");
                        E.Owner.Logger.WriteDebug($"Exception: {e.Message}");
                        return;
                    }
                }
            }
        }
    }
}
