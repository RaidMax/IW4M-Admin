using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Objects;
using SharedLibraryCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Commands
{
    public class CQuit : Command
    {
        public CQuit() :
            base("quit", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_QUIT_DESC"], "q", Player.Permission.Owner, false)
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            return Task.Run(() => { E.Owner.Manager.Stop(); });
        }
    }

    public class COwner : Command
    {
        public COwner() :
            base("owner", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_OWNER_DESC"], "iamgod", Player.Permission.User, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if ((await (E.Owner.Manager.GetClientService() as ClientService).GetOwners()).Count == 0)
            {
                E.Origin.Level = Player.Permission.Owner;
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_OWNER_SUCCESS"]);
                await E.Owner.Manager.GetClientService().Update(E.Origin);
            }
            else
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_OWNER_FAIL"]);
        }
    }

    public class CWarn : Command
    {
        public CWarn() :
            base("warn", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_WARN_DESC"], "w", Player.Permission.Trusted, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_PLAYER"],
                        Required = true
                    },
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_REASON"],
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Origin.Level <= E.Target.Level)
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_WARN_FAIL"]} {E.Target.Name}");
            else
                await E.Target.Warn(E.Data, E.Origin);
        }
    }

    public class CWarnClear : Command
    {
        public CWarnClear() :
            base("warnclear", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_WARNCLEAR_DESC"], "wc", Player.Permission.Trusted, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_PLAYER"],
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            E.Target.Warnings = 0;
            String Message = $"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_WARNCLEAR_SUCCESS"]} {E.Target.Name}";
            await E.Owner.Broadcast(Message);
        }
    }

    public class CKick : Command
    {
        public CKick() :
            base("kick", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_KICK_DESC"], "k", Player.Permission.Moderator, true, new CommandArgument[]
            {
                new CommandArgument()
                {
                    Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument()
                {
                    Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Origin.Level > E.Target.Level)
            {
                E.Owner.Manager.GetEventHandler().AddEvent(new GameEvent(GameEvent.EventType.Kick, E.Data, E.Origin, E.Target, E.Owner));
                await E.Target.Kick(E.Data, E.Origin);
                await E.Origin.Tell($"^5{E.Target} ^7{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_KICK_SUCCESS"]}");
            }
            else
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_KICK_FAIL"]} {E.Target.Name}");
        }
    }

    public class CSay : Command
    {
        public CSay() :
            base("say", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_SAY_DESC"], "s", Player.Permission.Moderator, false, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_MESSAGE"],
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Owner.Broadcast($"{(E.Owner.GameName == Server.Game.IW4 ? "^:" : "")}{E.Origin.Name} - ^6{E.Data}^7");
        }
    }

    public class CTempBan : Command
    {
        public CTempBan() :
            base("tempban", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_TEMPBAN_DESC"], "tb", Player.Permission.Administrator, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_PLAYER"],
                        Required = true
                    },
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_DURATION"],
                        Required = true,
                    },
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_REASON"],
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            String Message = Utilities.RemoveWords(E.Data, 1).Trim();
            var length = E.Data.Split(' ')[0].ToLower().ParseTimespan();
            if (length.TotalHours >= 1 && length.TotalHours < 2)
                Message = E.Data.Replace("1h", "").Replace("1H", "");

            if (E.Origin.Level > E.Target.Level)
            {
                await E.Target.TempBan(Message, length, E.Origin);
                await E.Origin.Tell($"^5{E.Target} ^7{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_TEMPBAN_SUCCESS"]} ^5{length.TimeSpanText()}");
            }
            else
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_TEMPBAN_FAIL"]} {E.Target.Name}");
        }
    }

    public class CBan : Command
    {
        public CBan() :
            base("ban", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_BAN_DESC"], "b", Player.Permission.SeniorAdmin, true, new CommandArgument[]
            {
                new CommandArgument()
                {
                    Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument()
                {
                    Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Origin.Level > E.Target.Level)
            {
                await E.Target.Ban(E.Data, E.Origin);
                await E.Origin.Tell($"^5{E.Target} ^7{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_BAN_SUCCESS"]}");
            }
            else
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_BAN_FAIL"]} {E.Target.Name}");
        }
    }

    public class CUnban : Command
    {
        public CUnban() :
            base("unban", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_UNBAN_DESC"], "ub", Player.Permission.SeniorAdmin, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_CLIENTID"],
                        Required = true,
                    },
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_REASON"],
                        Required  = true
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var penalties = await E.Owner.Manager.GetPenaltyService().GetActivePenaltiesAsync(E.Target.AliasLinkId);
            if (penalties.Where(p => p.Type == Penalty.PenaltyType.Ban || p.Type == Penalty.PenaltyType.TempBan).FirstOrDefault() != null)
            {
                await E.Owner.Unban(E.Data, E.Target, E.Origin);
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_UNBAN_SUCCESS"]} {E.Target}");
            }
            else
            {
                await E.Origin.Tell($"{E.Target} {Utilities.CurrentLocalization.LocalizationSet["COMMANDS_UNBAN_FAIL"]}");
            }
        }
    }

    public class CWhoAmI : Command
    {
        public CWhoAmI() :
            base("whoami", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_WHO_DESC"], "who", Player.Permission.User, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            String You = String.Format("{0} [^3#{1}^7] {2} [^3@{3}^7] [{4}^7] IP: {5}", E.Origin.Name, E.Origin.ClientNumber, E.Origin.NetworkId, E.Origin.ClientId, Utilities.ConvertLevelToColor(E.Origin.Level), E.Origin.IPAddressString);
            await E.Origin.Tell(You);
        }
    }

    public class CList : Command
    {
        public CList() :
            base("list", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_LIST_DESC"], "l", Player.Permission.Moderator, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
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
            base("help", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_HELP_DESC"], "h", Player.Permission.User, false, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_COMMANDS"],
                        Required = false
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            String cmd = E.Data.Trim();

            if (cmd.Length > 2)
            {
                bool found = false;
                foreach (Command C in E.Owner.Manager.GetCommands())
                {
                    if (C.Name == cmd.ToLower() ||
                        C.Alias == cmd.ToLower())
                    {
                        await E.Origin.Tell("[^3" + C.Name + "^7] " + C.Description);
                        await E.Origin.Tell(C.Syntax);
                        found = true;
                    }
                }

                if (!found)
                    await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_HELP_NOTFOUND"]);
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
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_HELP_MOREINFO"]);
            }
        }
    }

    public class CFastRestart : Command
    {
        public CFastRestart() :
            base("fastrestart", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_FASTRESTART_DESC"], "fr", Player.Permission.Moderator, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Owner.ExecuteCommandAsync("fast_restart");

            if (!E.Origin.Masked)
                await E.Owner.Broadcast($"^5{E.Origin.Name} ^7{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_FASTRESTART_UNMASKED"]}");
            else
                await E.Owner.Broadcast(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_FASTRESTART_MASKED"]);
        }
    }

    public class CMapRotate : Command
    {
        public CMapRotate() :
            base("maprotate", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_MAPROTATE_DESC"], "mr", Player.Permission.Administrator, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (!E.Origin.Masked)
                await E.Owner.Broadcast($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_MAPROTATE"]} [^5{E.Origin.Name}^7]");
            else
                await E.Owner.Broadcast(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_MAPROTATE"]);
            Task.Delay(5000).Wait();
            await E.Owner.ExecuteCommandAsync("map_rotate");
        }
    }

    public class CSetLevel : Command
    {
        public CSetLevel() :
            base("setlevel", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_SETLEVEL_DESC"], "sl", Player.Permission.Moderator, true, new CommandArgument[]
                {
                     new CommandArgument()
                     {
                         Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_PLAYER"],
                         Required = true
                     },
                     new CommandArgument()
                     {
                         Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_LEVEL"],
                         Required = true
                     }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Target == E.Origin)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_SETLEVEL_SELF"]);
                return;
            }

            Player.Permission newPerm = Utilities.MatchPermission(E.Data);

            if (newPerm == Player.Permission.Owner &&
                !E.Owner.Manager.GetApplicationSettings().Configuration().EnableMultipleOwners)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_SETLEVEL_OWNER"]);
                return;
            }

            if (E.Origin.Level < Player.Permission.Owner &&
                !E.Owner.Manager.GetApplicationSettings().Configuration().EnableSteppedHierarchy)
            {
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_SETLEVEL_STEPPEDDISABLED"]} ^5{E.Target.Name}");
                return;
            }

            if (newPerm >= E.Origin.Level)
            {
                if (E.Origin.Level < Player.Permission.Owner)
                {
                    await E.Origin.Tell(string.Format(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_SETLEVEL_LEVELTOOHIGH"], E.Target.Name, (E.Origin.Level - 1).ToString()));
                    return;
                }
            }

            else if (newPerm > Player.Permission.Banned)
            {
                var ActiveClient = E.Owner.Manager.GetActiveClients()
                    .FirstOrDefault(p => p.NetworkId == E.Target.NetworkId);

                if (ActiveClient != null)
                {
                    ActiveClient.Level = newPerm;
                    await ActiveClient.Tell($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_SETLEVEL_SUCCESS_TARGET"]} {newPerm}");
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
                    // this updates their privilege level to the webfront claims
                    E.Owner.Manager.GetPrivilegedClients()[E.Target.ClientId] = E.Target;
                }

                await E.Origin.Tell($"{E.Target.Name} {Utilities.CurrentLocalization.LocalizationSet["COMMANDS_SETLEVEL_SUCCESS"]}");
            }

            else
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_SETLEVEL_FAIL"]);
        }
    }

    public class CUsage : Command
    {
        public CUsage() :
            base("usage", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_USAGE_DESC"], "us", Player.Permission.Moderator, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Origin.Tell($"IW4MAdmin {Utilities.CurrentLocalization.LocalizationSet["COMMANDS_USAGE_TEXT"]}" + Math.Round(((System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 2048f) / 1200f), 1) + "MB");
        }
    }

    public class CUptime : Command
    {
        public CUptime() :
            base("uptime", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_UPTIME_DESC"], "up", Player.Permission.Moderator, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            TimeSpan uptime = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
            var loc = Utilities.CurrentLocalization.LocalizationSet;
            await E.Origin.Tell($"IW4M Admin {loc["COMMANDS_UPTIME_TEXT"]} {uptime.Days} {loc["GLOBAL_DAYS"]}, {uptime.Hours} {loc["GLOBAL_HOURS"]}, {uptime.Minutes} {loc["GLOBAL_MINUTES"]}");
        }
    }

    public class CListAdmins : Command
    {
        public CListAdmins() :
            base("admins", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ADMINS_DESC"], "a", Player.Permission.User, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
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
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ADMINS_NONE"]);
        }
    }

    public class CLoadMap : Command
    {
        public CLoadMap() :
            base("map", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_MAP_DESC"], "m", Player.Permission.Administrator, false, new CommandArgument[]
            {
                 new CommandArgument()
                 {
                     Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_MAP"],
                     Required = true
                 }
            })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            string newMap = E.Data.Trim().ToLower();
            foreach (Map m in E.Owner.Maps)
            {
                if (m.Name.ToLower() == newMap || m.Alias.ToLower() == newMap)
                {
                    await E.Owner.Broadcast($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_MAP_SUCCESS"]} ^5{m.Alias}");
                    Task.Delay(5000).Wait();
                    await E.Owner.LoadMap(m.Name);
                    return;
                }
            }

            await E.Owner.Broadcast($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_MAP_UKN"]} ^5{newMap}");
            Task.Delay(5000).Wait();
            await E.Owner.LoadMap(newMap);
        }
    }

    public class CFindPlayer : Command
    {
        public CFindPlayer() :
            base("find", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_FIND_DESC"], "f", Player.Permission.Administrator, false, new CommandArgument[]
            {
                new CommandArgument()
                {
                    Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_PLAYER"],
                    Required = true
                }
            })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Data.Length < 3)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_FIND_MIN"]);
                return;
            }

            IList<EFClient> db_players = (await (E.Owner.Manager.GetClientService() as ClientService)
                .GetClientByName(E.Data))
                .OrderByDescending(p => p.LastConnection)
                .ToList();

            if (db_players.Count == 0)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_FIND_EMPTY"]);
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
            base("rules", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_RULES_DESC"], "r", Player.Permission.User, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Owner.Manager.GetApplicationSettings().Configuration().GlobalRules?.Count < 1 &&
                E.Owner.ServerConfig.Rules?.Count < 1)
            {
                if (E.Message.IsBroadcastCommand())
                    await E.Owner.Broadcast(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_RULES_NONE"]);
                else
                    await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_RULES_NONE"]);
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
            base("privatemessage", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_PM_DESC"], "pm", Player.Permission.User, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_PLAYER"],
                        Required = true
                    },
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_MESSAGE"],
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Target.Tell($"^1{E.Origin.Name} ^3[PM]^7 - {E.Data}");
            await E.Origin.Tell($"To ^3{E.Target.Name} ^7-> {E.Data}");
        }
    }

    public class CFlag : Command
    {
        public CFlag() :
            base("flag", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_FLAG_DESC"], "fp", Player.Permission.Moderator, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_PLAYER"],
                        Required = true
                    },
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_REASON"],
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            // todo: move unflag to seperate command
            if (E.Target.Level >= E.Origin.Level)
            {
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_FLAG_FAIL"]} ^5{E.Target.Name}");
                return;
            }

            if (E.Target.Level == Player.Permission.Flagged)
            {
                E.Target.Level = Player.Permission.User;
                await E.Owner.Manager.GetClientService().Update(E.Target);
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_FLAG_UNFLAG"]} ^5{E.Target.Name}");
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
                E.Owner.Manager.GetEventHandler().AddEvent(new GameEvent(GameEvent.EventType.Flag, E.Data, E.Origin, E.Target, E.Owner));
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_FLAG_SUCCESS"]} ^5{E.Target.Name}");
            }

        }
    }

    public class CReport : Command
    {
        public CReport() :
            base("report", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_REPORT_DESC"], "rep", Player.Permission.User, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_PLAYER"],
                        Required = true
                    },
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_REASON"],
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Data.ToLower().Contains("camp"))
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_REPORT_FAIL_CAMP"]);
                return;
            }

            if (E.Owner.Reports.Find(x => (x.Origin == E.Origin && x.Target.NetworkId == E.Target.NetworkId)) != null)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_REPORT_FAIL_DUPLICATE"]);
                return;
            }

            if (E.Target == E.Origin)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_REPORT_FAIL_SELF"]);
                return;
            }

            if (E.Target.Level > E.Origin.Level)
            {
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_REPORT_FAIL"]} {E.Target.Name}");
                return;
            }

            E.Owner.Reports.Add(new Report(E.Target, E.Origin, E.Data));

            await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_REPORT_SUCCESS"]);
            E.Owner.Manager.GetEventHandler().AddEvent(new GameEvent(GameEvent.EventType.Report, E.Data, E.Origin, E.Target, E.Owner));
            await E.Owner.ToAdmins(String.Format("^5{0}^7->^1{1}^7: {2}", E.Origin.Name, E.Target.Name, E.Data));
        }
    }

    public class CListReports : Command
    {
        public CListReports() :
            base("reports", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_REPORTS_DESC"], "reps", Player.Permission.Moderator, false, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_CLEAR"],
                        Required = false
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Data != null && E.Data.ToLower().Contains(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_CLEAR"]))
            {
                E.Owner.Reports = new List<Report>();
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_REPORTS_CLEAR_SUCCESS"]);
                return;
            }

            if (E.Owner.Reports.Count < 1)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_REPORTS_NONE"]);
                return;
            }

            foreach (Report R in E.Owner.Reports)
                await E.Origin.Tell(String.Format("^5{0}^7->^1{1}^7: {2}", R.Origin.Name, R.Target.Name, R.Reason));
        }
    }

    public class CMask : Command
    {
        public CMask() :
            base("mask", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_MASK_DESC"], "hide", Player.Permission.Moderator, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Origin.Masked)
            {
                E.Origin.Masked = false;
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_MASK_OFF"]);
            }
            else
            {
                E.Origin.Masked = true;
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_MASK_ON"]);
            }

            await E.Owner.Manager.GetClientService().Update(E.Origin);
        }
    }

    public class CListBanInfo : Command
    {
        public CListBanInfo() :
            base("baninfo", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_BANINFO_DESC"], "bi", Player.Permission.Moderator, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_PLAYER"],
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var B = await E.Owner.Manager.GetPenaltyService().GetClientPenaltiesAsync(E.Target.ClientId);

            var penalty = B.FirstOrDefault(b => b.Type > Penalty.PenaltyType.Kick && b.Expires > DateTime.UtcNow);

            if (penalty == null)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_BANINFO_NONE"]);
                return;
            }

            string timeRemaining = penalty.Type == Penalty.PenaltyType.TempBan ? $"({(penalty.Expires - DateTime.UtcNow).TimeSpanText()} remaining)" : "";
            string success = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_BANINO_SUCCESS"];

            await E.Origin.Tell($"^1{E.Target.Name} ^7{string.Format(success, penalty.Punisher.Name)} {penalty.Punisher.Name} {timeRemaining}");
        }

    }

    public class CListAlias : Command
    {
        public CListAlias() :
            base("alias", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ALIAS_DESC"], "known", Player.Permission.Moderator, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_PLAYER"],
                        Required = true,
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            StringBuilder message = new StringBuilder();
            var names = new List<string>(E.Target.AliasLink.Children.Select(a => a.Name));
            var IPs = new List<string>(E.Target.AliasLink.Children.Select(a => a.IPAddress.ConvertIPtoString()).Distinct());

            await E.Target.Tell($"[^3{E.Target}^7]");

            message.Append($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ALIAS_ALIASES"]}: ");
            message.Append(String.Join(" | ", names));
            await E.Origin.Tell(message.ToString());

            message.Clear();
            message.Append($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ALIAS_IPS"]}: ");
            message.Append(String.Join(" | ", IPs));
            await E.Origin.Tell(message.ToString());
        }
    }

    public class CExecuteRCON : Command
    {
        public CExecuteRCON() :
            base("rcon", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_RCON_DESC"], "rcon", Player.Permission.Owner, false, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_COMMANDS"],
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var Response = await E.Owner.ExecuteCommandAsync(E.Data.Trim());
            foreach (string S in Response)
                await E.Origin.Tell(S.StripColors());
            if (Response.Length == 0)
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_RCON_SUCCESS"]);
        }
    }

    public class CPlugins : Command
    {
        public CPlugins() :
            base("plugins", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_PLUGINS_DESC"], "p", Player.Permission.Administrator, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_PLUGINS_LOADED"]);
            foreach (var P in Plugins.PluginImporter.ActivePlugins)
            {
                await E.Origin.Tell(String.Format("^3{0} ^7[v^3{1}^7] by ^5{2}^7", P.Name, P.Version, P.Author));
            }
        }
    }

    public class CIP : Command
    {
        public CIP() :
            base("getexternalip", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_IP_DESC"], "ip", Player.Permission.User, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_IP_SUCCESS"]} ^5{E.Origin.IPAddressString}");
        }
    }

    public class CPruneAdmins : Command
    {
        public CPruneAdmins() : base("prune", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_PRUNE_DESC"], "pa", Player.Permission.Owner, false, new CommandArgument[]
        {
            new CommandArgument()
            {
                Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_INACTIVE"],
                Required = false
            }
        })
        { }

        public override async Task ExecuteAsync(GameEvent E)
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
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_PRUNE_FAIL"]);
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
            await E.Origin.Tell($"^5{inactiveUsers.Count} ^7{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_PRUNE_SUCCESS"]}");

        }
    }

    public class CSetPassword : Command
    {
        public CSetPassword() : base("setpassword", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_SETPASSWORD_DESC"], "sp", Player.Permission.Moderator, false, new CommandArgument[]
            {
                new CommandArgument()
                {
                    Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_PASSWORD"],
                    Required = true
                }
            })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Data.Length < 5)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_PASSWORD_FAIL"]);
                return;
            }

            string[] hashedPassword = Helpers.Hashing.Hash(E.Data);

            E.Origin.Password = hashedPassword[0];
            E.Origin.PasswordSalt = hashedPassword[1];

            // update the password for the client in privileged
            E.Owner.Manager.GetPrivilegedClients()[E.Origin.ClientId].Password = hashedPassword[0];
            E.Owner.Manager.GetPrivilegedClients()[E.Origin.ClientId].PasswordSalt = hashedPassword[1];

            await E.Owner.Manager.GetClientService().Update(E.Origin);
            await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationSet["COMMANDS_PASSWORD_SUCCESS"]);
        }
    }

    /*
    public class CKillServer : Command
    {
        public CKillServer() : base("killserver", "kill the game server", "kill", Player.Permission.Administrator, false)
        {
        }

        public override async Task ExecuteAsync(GameEvent E)
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
    }*/


    public class CPing : Command
    {
        public CPing() : base("ping", Utilities.CurrentLocalization.LocalizationSet["COMMANDS_PING_DESC"], "pi", Player.Permission.User, false, new CommandArgument[]
        {
            new CommandArgument()
            {
                Name = Utilities.CurrentLocalization.LocalizationSet["COMMANDS_ARGS_PLAYER"],
                Required = false
            }
        })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Message.IsBroadcastCommand())
            {
                if (E.Target == null)
                    await E.Owner.Broadcast($"{E.Origin.Name}'s {Utilities.CurrentLocalization.LocalizationSet["COMMANDS_PING_TARGET"]} ^5{E.Origin.Ping}^7ms");
                else
                    await E.Owner.Broadcast($"{E.Target.Name}'s {Utilities.CurrentLocalization.LocalizationSet["COMMANDS_PING_TARGET"]} ^5{E.Target.Ping}^7ms");
            }
            else
            {
                if (E.Target == null)
                    await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationSet["COMMANDS_PING_SELF"]} ^5{E.Origin.Ping}^7ms");
                else
                    await E.Origin.Tell($"{E.Target.Name}'s {Utilities.CurrentLocalization.LocalizationSet["COMMANDS_PING_TARGET"]} ^5{E.Target.Ping}^7ms");
            }
        }
    }
}
