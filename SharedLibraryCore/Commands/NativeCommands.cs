using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Events;
using SharedLibraryCore.Objects;
using SharedLibraryCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static SharedLibraryCore.RCon.StaticHelpers;

namespace SharedLibraryCore.Commands
{
    public class CQuit : Command
    {
        public CQuit() :
            base("quit", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_QUIT_DESC"], "q", Player.Permission.Owner, false)
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            return Task.Run(() => { E.Owner.Manager.Stop(); });
        }
    }

    public class COwner : Command
    {
        public COwner() :
            base("owner", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_OWNER_DESC"], "iamgod", Player.Permission.User, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if ((await (E.Owner.Manager.GetClientService() as ClientService).GetOwners()).Count == 0)
            {
                E.Origin.Level = Player.Permission.Owner;
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_OWNER_SUCCESS"]);
                // so setpassword/login works
                E.Owner.Manager.GetPrivilegedClients().Add(E.Origin.ClientId, E.Origin);
                await E.Owner.Manager.GetClientService().Update(E.Origin);
            }
            else
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_OWNER_FAIL"]);
        }
    }

    public class CWarn : Command
    {
        public CWarn() :
            base("warn", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_WARN_DESC"], "w", Player.Permission.Trusted, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
                        Required = true
                    },
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_REASON"],
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Origin.Level <= E.Target.Level)
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_WARN_FAIL"]} {E.Target.Name}");
            else
            {
                await E.Target.Warn(E.Data, E.Origin);

                var e = new GameEvent()
                {
                    Type = GameEvent.EventType.Warn,
                    Data = E.Data,
                    Origin = E.Origin,
                    Target = E.Target,
                    Owner = E.Owner
                };

                E.Owner.Manager.GetEventHandler().AddEvent(e);
            }
        }
    }

    public class CWarnClear : Command
    {
        public CWarnClear() :
            base("warnclear", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_WARNCLEAR_DESC"], "wc", Player.Permission.Trusted, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            E.Target.Warnings = 0;
            String Message = $"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_WARNCLEAR_SUCCESS"]} {E.Target.Name}";
            await E.Owner.Broadcast(Message);
        }
    }

    public class CKick : Command
    {
        public CKick() :
            base("kick", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_KICK_DESC"], "k", Player.Permission.Moderator, true, new CommandArgument[]
            {
                new CommandArgument()
                {
                    Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument()
                {
                    Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Origin.Level > E.Target.Level)
            {
                await E.Target.Kick(E.Data, E.Origin);
                await E.Origin.Tell($"^5{E.Target} ^7{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_KICK_SUCCESS"]}");

                var e = new GameEvent()
                {
                    Type = GameEvent.EventType.Kick,
                    Data = E.Data,
                    Origin = E.Origin,
                    Target = E.Target,
                    Owner = E.Owner
                };

                E.Owner.Manager.GetEventHandler().AddEvent(e);
            }
            else
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_KICK_FAIL"]} {E.Target.Name}");
        }
    }

    public class CSay : Command
    {
        public CSay() :
            base("say", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SAY_DESC"], "s", Player.Permission.Moderator, false, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_MESSAGE"],
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
            base("tempban", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_TEMPBAN_DESC"], "tb", Player.Permission.Administrator, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
                        Required = true
                    },
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_DURATION"],
                        Required = true,
                    },
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_REASON"],
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
                await E.Origin.Tell($"^5{E.Target} ^7{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_TEMPBAN_SUCCESS"]} ^5{length.TimeSpanText()}");

                var e = new GameEvent()
                {
                    Type = GameEvent.EventType.TempBan,
                    Data = E.Data,
                    Origin = E.Origin,
                    Target = E.Target,
                    Owner = E.Owner
                };

                E.Owner.Manager.GetEventHandler().AddEvent(e);
            }
            else
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_TEMPBAN_FAIL"]} {E.Target.Name}");
        }
    }

    public class CBan : Command
    {
        public CBan() :
            base("ban", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_BAN_DESC"], "b", Player.Permission.SeniorAdmin, true, new CommandArgument[]
            {
                new CommandArgument()
                {
                    Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument()
                {
                    Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Origin.Level > E.Target.Level)
            {
                await E.Target.Ban(E.Data, E.Origin);
                await E.Origin.Tell($"^5{E.Target} ^7{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_BAN_SUCCESS"]}");
            }
            else
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_BAN_FAIL"]} {E.Target.Name}");
        }
    }

    public class CUnban : Command
    {
        public CUnban() :
            base("unban", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_UNBAN_DESC"], "ub", Player.Permission.SeniorAdmin, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_CLIENTID"],
                        Required = true,
                    },
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_REASON"],
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
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_UNBAN_SUCCESS"]} {E.Target}");
            }
            else
            {
                await E.Origin.Tell($"{E.Target} {Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_UNBAN_FAIL"]}");
            }
        }
    }

    public class CWhoAmI : Command
    {
        public CWhoAmI() :
            base("whoami", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_WHO_DESC"], "who", Player.Permission.User, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            String You = String.Format("{0} [^3#{1}^7] {2} [^3@{3}^7] [{4}^7] IP: {5}", E.Origin.Name, E.Origin.ClientNumber, E.Origin.NetworkId, E.Origin.ClientId, Utilities.ConvertLevelToColor(E.Origin.Level, E.Origin.ClientPermission.Name), E.Origin.IPAddressString);
            await E.Origin.Tell(You);
        }
    }

    public class CList : Command
    {
        public CList() :
            base("list", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_LIST_DESC"], "l", Player.Permission.Moderator, false)
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
                // todo: fix spacing
                // todo: make this better :)
                if (P.Masked)
                    playerList.AppendFormat("[^3{0}^7]{3}[^3{1}^7] {2}", Utilities.ConvertLevelToColor(Player.Permission.User, P.ClientPermission.Name), P.ClientNumber, P.Name, Utilities.GetSpaces(Player.Permission.SeniorAdmin.ToString().Length - Player.Permission.User.ToString().Length));
                else
                    playerList.AppendFormat("[^3{0}^7]{3}[^3{1}^7] {2}", Utilities.ConvertLevelToColor(P.Level, P.ClientPermission.Name), P.ClientNumber, P.Name, Utilities.GetSpaces(Player.Permission.SeniorAdmin.ToString().Length - P.Level.ToString().Length));

                if (count == 2 || E.Owner.GetPlayersAsList().Count == 1)
                {
                    await E.Origin.Tell(playerList.ToString());
                    await Task.Delay(FloodProtectionInterval);
                    count = 0;
                    playerList = new StringBuilder();
                    continue;
                }

                count++;
            }

            if (playerList.Length > 0)
            {
                await E.Origin.Tell(playerList.ToString());
            }
        }
    }

    public class CHelp : Command
    {
        public CHelp() :
            base("help", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_HELP_DESC"], "h", Player.Permission.User, false, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_COMMANDS"],
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
                        await Task.Delay(FloodProtectionInterval);
                        found = true;
                    }
                }

                if (!found)
                    await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_HELP_NOTFOUND"]);
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
                            await Task.Delay(FloodProtectionInterval);
                            helpResponse = new StringBuilder();
                            count = 0;
                        }
                        count++;
                    }
                }
                await E.Origin.Tell(helpResponse.ToString());
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_HELP_MOREINFO"]);
            }
        }
    }

    public class CFastRestart : Command
    {
        public CFastRestart() :
            base("fastrestart", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FASTRESTART_DESC"], "fr", Player.Permission.Moderator, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Owner.ExecuteCommandAsync("fast_restart");

            if (!E.Origin.Masked)
                await E.Owner.Broadcast($"^5{E.Origin.Name} ^7{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FASTRESTART_UNMASKED"]}");
            else
                await E.Owner.Broadcast(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FASTRESTART_MASKED"]);
        }
    }

    public class CMapRotate : Command
    {
        public CMapRotate() :
            base("maprotate", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MAPROTATE_DESC"], "mr", Player.Permission.Administrator, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (!E.Origin.Masked)
                await E.Owner.Broadcast($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MAPROTATE"]} [^5{E.Origin.Name}^7]");
            else
                await E.Owner.Broadcast(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MAPROTATE"]);
            await Task.Delay(5000);
            await E.Owner.ExecuteCommandAsync("map_rotate");
        }
    }

    public class CSetLevel : Command
    {
        public CSetLevel() :
            base("setlevel", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_DESC"], "sl", Player.Permission.Moderator, true, new CommandArgument[]
                {
                     new CommandArgument()
                     {
                         Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
                         Required = true
                     },
                     new CommandArgument()
                     {
                         Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_LEVEL"],
                         Required = true
                     }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Target == E.Origin)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_SELF"]);
                return;
            }

            Player.Permission oldPerm = E.Target.Level;
            Player.Permission newPerm = Utilities.MatchPermission(E.Data);

            if (newPerm == Player.Permission.Owner &&
                !E.Owner.Manager.GetApplicationSettings().Configuration().EnableMultipleOwners)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_OWNER"]);
                return;
            }

            if (E.Origin.Level < Player.Permission.Owner &&
                !E.Owner.Manager.GetApplicationSettings().Configuration().EnableSteppedHierarchy)
            {
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_STEPPEDDISABLED"]} ^5{E.Target.Name}");
                return;
            }

            if (newPerm >= E.Origin.Level)
            {
                if (E.Origin.Level < Player.Permission.Owner)
                {
                    await E.Origin.Tell(string.Format(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_LEVELTOOHIGH"], E.Target.Name, (E.Origin.Level - 1).ToString()));
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
                    await E.Owner.Manager.GetClientService().Update(ActiveClient);
                    await ActiveClient.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_SUCCESS_TARGET"]} {newPerm}");
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

                var e = new GameEvent()
                {
                    Origin = E.Origin,
                    Target = E.Target,
                    Owner = E.Owner,
                    Type = GameEvent.EventType.ChangePermission,
                    Extra = new Change()
                    {
                        PreviousValue = oldPerm.ToString(),
                        NewValue = newPerm.ToString()
                    }
                };

                E.Owner.Manager.GetEventHandler().AddEvent(e);

                await E.Origin.Tell($"{E.Target.Name} {Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_SUCCESS"]}");
            }

            else
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_FAIL"]);
        }
    }

    public class CUsage : Command
    {
        public CUsage() :
            base("usage", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_USAGE_DESC"], "us", Player.Permission.Moderator, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Origin.Tell($"IW4MAdmin {Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_USAGE_TEXT"]}" + Math.Round(((System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 2048f) / 1200f), 1) + "MB");
        }
    }

    public class CUptime : Command
    {
        public CUptime() :
            base("uptime", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_UPTIME_DESC"], "up", Player.Permission.Moderator, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            TimeSpan uptime = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
            var loc = Utilities.CurrentLocalization.LocalizationIndex;
            await E.Origin.Tell($"IW4M Admin {loc["COMMANDS_UPTIME_TEXT"]} {uptime.Days} {loc["GLOBAL_TIME_DAYS"]}, {uptime.Hours} {loc["GLOBAL_TIME_HOURS"]}, {uptime.Minutes} {loc["GLOBAL_TIME_MINUTES"]}");
        }
    }

    public class CListAdmins : Command
    {
        public CListAdmins() :
            base("admins", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ADMINS_DESC"], "a", Player.Permission.User, false)
        { }

        public static string OnlineAdmins(Server S)
        {
            var onlineAdmins = S.GetPlayersAsList()
                .Where(p => p.Level > Player.Permission.Flagged)
                .Where(p => !p.Masked)
                .Select(p => $"[^3{Utilities.ConvertLevelToColor(p.Level, p.ClientPermission.Name)}^7] {p.Name}");

            return onlineAdmins.Count() > 0 ?
                string.Join(Environment.NewLine, onlineAdmins) :
                Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ADMINS_NONE"];
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            foreach (string line in OnlineAdmins(E.Owner).Split(Environment.NewLine))
            {
                var t = E.Message.IsBroadcastCommand() ? E.Owner.Broadcast(line) : E.Origin.Tell(line);
                await t;

                await Task.Delay(FloodProtectionInterval);
            }
        }
    }

    public class CLoadMap : Command
    {
        public CLoadMap() :
            base("map", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MAP_DESC"], "m", Player.Permission.Administrator, false, new CommandArgument[]
            {
                 new CommandArgument()
                 {
                     Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_MAP"],
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
                    await E.Owner.Broadcast($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MAP_SUCCESS"]} ^5{m.Alias}");
                    await Task.Delay(5000);
                    await E.Owner.LoadMap(m.Name);
                    return;
                }
            }

            await E.Owner.Broadcast($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MAP_UKN"]} ^5{newMap}");
            await Task.Delay(5000);
            await E.Owner.LoadMap(newMap);
        }
    }

    public class CFindPlayer : Command
    {
        public CFindPlayer() :
            base("find", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FIND_DESC"], "f", Player.Permission.Administrator, false, new CommandArgument[]
            {
                new CommandArgument()
                {
                    Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
                    Required = true
                }
            })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Data.Length < 3)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FIND_MIN"]);
                return;
            }

            IList<EFClient> db_players = (await (E.Owner.Manager.GetClientService() as ClientService)
                .GetClientByName(E.Data))
                .OrderByDescending(p => p.LastConnection)
                .ToList();

            if (db_players.Count == 0)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FIND_EMPTY"]);
                return;
            }

            foreach (var P in db_players)
            {
                string localizedLevel = Utilities.CurrentLocalization.LocalizationIndex[$"GLOBAL_PERMISSION_{P.Level.ToString().ToUpper()}"];
                // they're not going by another alias
                string msg = P.Name.ToLower().Contains(E.Data.ToLower()) ?
                    $"[^3{P.Name}^7] [^3@{P.ClientId}^7] - [{ Utilities.ConvertLevelToColor(P.Level, localizedLevel)}^7] - {P.IPAddressString} | last seen {Utilities.GetTimePassed(P.LastConnection)}" :
                    $"({P.AliasLink.Children.FirstOrDefault(a => a.Name.ToLower().Contains(E.Data.ToLower()))?.Name})->[^3{P.Name}^7] [^3@{P.ClientId}^7] - [{ Utilities.ConvertLevelToColor(P.Level, localizedLevel)}^7] - {P.IPAddressString} | last seen {Utilities.GetTimePassed(P.LastConnection)}";
                await E.Origin.Tell(msg);
                await Task.Delay(FloodProtectionInterval);
            }
        }
    }

    public class CListRules : Command
    {
        public CListRules() :
            base("rules", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_RULES_DESC"], "r", Player.Permission.User, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Owner.Manager.GetApplicationSettings().Configuration().GlobalRules?.Count < 1 &&
                E.Owner.ServerConfig.Rules?.Count < 1)
            {
                if (E.Message.IsBroadcastCommand())
                    await E.Owner.Broadcast(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_RULES_NONE"]);
                else
                    await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_RULES_NONE"]);
            }

            else
            {
                var rules = new List<string>();
                rules.AddRange(E.Owner.Manager.GetApplicationSettings().Configuration().GlobalRules);
                if (E.Owner.ServerConfig.Rules != null)
                    rules.AddRange(E.Owner.ServerConfig.Rules);

                foreach (string r in rules)
                {
                    var t = E.Message.IsBroadcastCommand() ? E.Owner.Broadcast($"- {r}") : E.Origin.Tell($"- {r}");
                    await t;
                    await Task.Delay(FloodProtectionInterval);
                }
            }
        }
    }

    public class CPrivateMessage : Command
    {
        public CPrivateMessage() :
            base("privatemessage", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PM_DESC"], "pm", Player.Permission.User, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
                        Required = true
                    },
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_MESSAGE"],
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
            base("flag", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FLAG_DESC"], "fp", Player.Permission.Moderator, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
                        Required = true
                    },
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_REASON"],
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Target.Level >= E.Origin.Level)
            {
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FLAG_FAIL"]} ^5{E.Target.Name}");
                return;
            }

            if (E.Target.Level == Player.Permission.Flagged)
            {
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FLAG_ALREADYFLAGGED"]}");
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

                var e = new GameEvent()
                {
                    Type = GameEvent.EventType.Flag,
                    Data = E.Data,
                    Origin = E.Origin,
                    Target = E.Target,
                    Owner = E.Owner
                };

                E.Owner.Manager.GetEventHandler().AddEvent(e);
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FLAG_SUCCESS"]} ^5{E.Target.Name}");
            }

        }
    }

    public class CUnflag : Command
    {
        public CUnflag() :
            base("unflag", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_UNFLAG_DESC"], "uf", Player.Permission.Moderator, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Target.Level >= E.Origin.Level)
            {
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_UNFLAG_FAIL"]} ^5{E.Target.Name}");
                return;
            }

            if (E.Target.Level == Player.Permission.Flagged)
            {
                E.Target.Level = Player.Permission.User;
                await E.Owner.Manager.GetClientService().Update(E.Target);
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FLAG_UNFLAG"]} ^5{E.Target.Name}");

                var e = new GameEvent()
                {
                    Data = E.Data,
                    Origin = E.Origin,
                    Target = E.Target,
                    Owner = E.Owner,
                    Type = GameEvent.EventType.Unflag
                };

                E.Owner.Manager.GetEventHandler().AddEvent(e);
            }

            else
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_UNFLAG_NOTFLAGGED"]);
            }
        }
    }


    public class CReport : Command
    {
        public CReport() :
            base("report", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORT_DESC"], "rep", Player.Permission.User, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
                        Required = true
                    },
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_REASON"],
                        Required = true
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Data.ToLower().Contains("camp"))
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORT_FAIL_CAMP"]);
                return;
            }

            if (E.Owner.Reports.Find(x => (x.Origin == E.Origin && x.Target.NetworkId == E.Target.NetworkId)) != null)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORT_FAIL_DUPLICATE"]);
                return;
            }

            if (E.Target == E.Origin)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORT_FAIL_SELF"]);
                return;
            }

            if (E.Target.Level > E.Origin.Level)
            {
                await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORT_FAIL"]} {E.Target.Name}");
                return;
            }

            E.Owner.Reports.Add(new Report(E.Target, E.Origin, E.Data));

            Penalty newReport = new Penalty()
            {
                Type = Penalty.PenaltyType.Report,
                Expires = DateTime.UtcNow,
                Offender = E.Target,
                Offense = E.Data,
                Punisher = E.Origin,
                Active = true,
                When = DateTime.UtcNow,
                Link = E.Target.AliasLink
            };

            await E.Owner.Manager.GetPenaltyService().Create(newReport);

            await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORT_SUCCESS"]);

            var e = new GameEvent()
            {
                Type = GameEvent.EventType.Report,
                Data = E.Data,
                Origin = E.Origin,
                Target = E.Target,
                Owner = E.Owner
            };

            E.Owner.Manager.GetEventHandler().AddEvent(e);
            await E.Owner.ToAdmins(String.Format("^5{0}^7->^1{1}^7: {2}", E.Origin.Name, E.Target.Name, E.Data));
        }
    }

    public class CListReports : Command
    {
        public CListReports() :
            base("reports", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORTS_DESC"], "reps", Player.Permission.Moderator, false, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_CLEAR"],
                        Required = false
                    }
                })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Data != null && E.Data.ToLower().Contains(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_CLEAR"]))
            {
                E.Owner.Reports = new List<Report>();
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORTS_CLEAR_SUCCESS"]);
                return;
            }

            if (E.Owner.Reports.Count < 1)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORTS_NONE"]);
                return;
            }

            foreach (Report R in E.Owner.Reports)
            {
                await E.Origin.Tell(String.Format("^5{0}^7->^1{1}^7: {2}", R.Origin.Name, R.Target.Name, R.Reason));
                await Task.Delay(FloodProtectionInterval);
            }
        }
    }

    public class CMask : Command
    {
        public CMask() :
            base("mask", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MASK_DESC"], "hide", Player.Permission.Moderator, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Origin.Masked)
            {
                E.Origin.Masked = false;
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MASK_OFF"]);
            }
            else
            {
                E.Origin.Masked = true;
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MASK_ON"]);
            }

            await E.Owner.Manager.GetClientService().Update(E.Origin);
        }
    }

    public class CListBanInfo : Command
    {
        public CListBanInfo() :
            base("baninfo", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_BANINFO_DESC"], "bi", Player.Permission.Moderator, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
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
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_BANINFO_NONE"]);
                return;
            }

            string timeRemaining = penalty.Type == Penalty.PenaltyType.TempBan ? $"({(penalty.Expires - DateTime.UtcNow).TimeSpanText()} remaining)" : "";
            string success = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_BANINFO_SUCCESS"];

            await E.Origin.Tell($"^1{E.Target.Name} ^7{string.Format(success, penalty.Punisher.Name)} {penalty.Punisher.Name} {timeRemaining}");
        }

    }

    public class CListAlias : Command
    {
        public CListAlias() :
            base("alias", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ALIAS_DESC"], "known", Player.Permission.Moderator, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
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

            message.Append($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ALIAS_ALIASES"]}: ");
            message.Append(String.Join(" | ", names));
            await E.Origin.Tell(message.ToString());

            message.Clear();
            message.Append($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ALIAS_IPS"]}: ");
            message.Append(String.Join(" | ", IPs));
            await E.Origin.Tell(message.ToString());
        }
    }

    public class CExecuteRCON : Command
    {
        public CExecuteRCON() :
            base("rcon", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_RCON_DESC"], "rcon", Player.Permission.Owner, false, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_COMMANDS"],
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
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_RCON_SUCCESS"]);
        }
    }

    public class CPlugins : Command
    {
        public CPlugins() :
            base("plugins", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PLUGINS_DESC"], "p", Player.Permission.Administrator, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PLUGINS_LOADED"]);
            foreach (var P in Plugins.PluginImporter.ActivePlugins)
            {
                await E.Origin.Tell(String.Format("^3{0} ^7[v^3{1}^7] by ^5{2}^7", P.Name, P.Version, P.Author));
                await Task.Delay(FloodProtectionInterval);
            }
        }
    }

    public class CIP : Command
    {
        public CIP() :
            base("getexternalip", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_IP_DESC"], "ip", Player.Permission.User, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_IP_SUCCESS"]} ^5{E.Origin.IPAddressString}");
        }
    }

    public class CPruneAdmins : Command
    {
        public CPruneAdmins() : base("prune", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PRUNE_DESC"], "pa", Player.Permission.Owner, false, new CommandArgument[]
        {
            new CommandArgument()
            {
                Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_INACTIVE"],
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
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PRUNE_FAIL"]);
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
            await E.Origin.Tell($"^5{inactiveUsers.Count} ^7{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PRUNE_SUCCESS"]}");

        }
    }

    public class CSetPassword : Command
    {
        public CSetPassword() : base("setpassword", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETPASSWORD_DESC"], "sp", Player.Permission.Moderator, false, new CommandArgument[]
            {
                new CommandArgument()
                {
                    Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PASSWORD"],
                    Required = true
                }
            })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Data.Length < 5)
            {
                await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PASSWORD_FAIL"]);
                return;
            }

            string[] hashedPassword = Helpers.Hashing.Hash(E.Data);

            E.Origin.Password = hashedPassword[0];
            E.Origin.PasswordSalt = hashedPassword[1];

            // update the password for the client in privileged
            E.Owner.Manager.GetPrivilegedClients()[E.Origin.ClientId].Password = hashedPassword[0];
            E.Owner.Manager.GetPrivilegedClients()[E.Origin.ClientId].PasswordSalt = hashedPassword[1];

            await E.Owner.Manager.GetClientService().Update(E.Origin);
            await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PASSWORD_SUCCESS"]);
        }
    }

    public class CKillServer : Command
    {
        public CKillServer() : base("killserver", "kill the game server", "kill", Player.Permission.Administrator, false)
        {
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var gameserverProcesses = System.Diagnostics.Process.GetProcessesByName("iw4x");

            System.Diagnostics.Process currentProcess = null;

            foreach (var p in gameserverProcesses)
            {
                string cmdLine = Utilities.GetCommandLine(p.Id);

                var regex = Regex.Match(cmdLine, @".*((?:\+set|\+) net_port) +([0-9]+).*");

                if (regex.Success && Int32.Parse(regex.Groups[2].Value) == E.Owner.GetPort())
                {
                    currentProcess = p;
                }
            }


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

                catch (Exceptions.NetworkException)
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


    public class CPing : Command
    {
        public CPing() : base("ping", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PING_DESC"], "pi", Player.Permission.User, false, new CommandArgument[]
        {
            new CommandArgument()
            {
                Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
                Required = false
            }
        })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Message.IsBroadcastCommand())
            {
                if (E.Target == null)
                    await E.Owner.Broadcast($"{E.Origin.Name}'s {Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PING_TARGET"]} ^5{E.Origin.Ping}^7ms");
                else
                    await E.Owner.Broadcast($"{E.Target.Name}'s {Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PING_TARGET"]} ^5{E.Target.Ping}^7ms");
            }
            else
            {
                if (E.Target == null)
                    await E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PING_SELF"]} ^5{E.Origin.Ping}^7ms");
                else
                    await E.Origin.Tell($"{E.Target.Name}'s {Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PING_TARGET"]} ^5{E.Target.Ping}^7ms");
            }
        }
    }

    public class CSetGravatar : Command
    {
        public CSetGravatar() : base("setgravatar", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_GRAVATAR_DESC"], "sg", Player.Permission.User, false, new CommandArgument[]
        {
            new CommandArgument()
            {
                Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_GRAVATAR"],
                Required = true
            }
        })
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            using (var ctx = new DatabaseContext())
            {
                var iqMeta = from meta in ctx.EFMeta
                             where meta.ClientId == E.Origin.ClientId
                             where meta.Key == "GravatarEmail"
                             select meta;

                var gravatarMeta = await iqMeta.FirstOrDefaultAsync();

                // gravatar meta has never been added
                if (gravatarMeta == null)
                {
                    using (var md5 = MD5.Create())
                    {
                        gravatarMeta = new EFMeta()
                        {
                            Active = true,
                            ClientId = E.Origin.ClientId,
                            Key = "GravatarEmail",
                            Value = string.Concat(md5.ComputeHash(E.Data.ToLower().Select(d => Convert.ToByte(d)).ToArray())
                                .Select(h => h.ToString("x2"))),
                        };

                        ctx.EFMeta.Add(gravatarMeta);
                        await ctx.SaveChangesAsync();
                        await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_GRAVATAR_SUCCESS_NEW"]);
                        return;
                    }
                }

                else
                {
                    ctx.EFMeta.Update(gravatarMeta);
                    using (var md5 = MD5.Create())
                    {
                        gravatarMeta.Value = string.Concat(md5.ComputeHash(E.Data.ToLower().Select(d => Convert.ToByte(d)).ToArray())
                                .Select(h => h.ToString("x2")));
                        gravatarMeta.Updated = DateTime.UtcNow;
                    }

                    await ctx.SaveChangesAsync();
                    await E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_GRAVATAR_SUCCESS_UPDATE"]);
                }
            }
        }
    }

    /// <summary>
    /// Retrieves the next map in rotation
    /// </summary>
    public class CNextMap : Command
    {
        public CNextMap() : base("nextmap", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_NEXTMAP_DESC"], "nm", Player.Permission.User, false) { }
        public static async Task<string> GetNextMap(Server s)
        {
            string mapRotation = (await s.GetDvarAsync<string>("sv_mapRotation")).Value.ToLower();
            var regexMatches = Regex.Matches(mapRotation, @"(gametype +([a-z]{1,4}))? *map ([a-z|_]+)", RegexOptions.IgnoreCase).ToList();

            // find the current map in the rotation
            var currentMap = regexMatches.Where(m => m.Groups[3].ToString() == s.CurrentMap.Name);
            var lastMap = regexMatches.LastOrDefault();
            Map nextMap = null;

            // no maprotation at all
            if (regexMatches.Count() == 0)
            {
                return $"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_NEXTMAP_SUCCESS"]} ^5{s.CurrentMap.Alias}/{Utilities.GetLocalizedGametype(s.Gametype)}";
            }

            // the current map is not in rotation
            if (currentMap.Count() == 0)
            {
                return Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_NEXTMAP_NOT_IN_ROTATION"];
            }

            // there's duplicate maps in rotation
            else if (currentMap.Count() > 1)
            {
                // gametype has been manually specified
                var duplicateMaps = currentMap.Where(m => !string.IsNullOrEmpty(m.Groups[1].ToString()));

                // more than one instance of map in rotation
                if (duplicateMaps.Count() > 0)
                {
                    currentMap = duplicateMaps.Where(m => m.Groups[2].ToString() == s.Gametype);
                }

                // else we just have to assume it's the first one
            }

            // if the current map is the last map, the next map is the first map
            var nextMapMatch = currentMap.First().Index != lastMap.Index ?
               regexMatches[regexMatches.IndexOf(currentMap.First()) + 1] :
               regexMatches.First();
            nextMap = s.Maps.FirstOrDefault(m => m.Name == nextMapMatch.Groups[3].ToString()) ?? nextMap;
            string nextGametype = nextMapMatch.Groups[2].ToString().Length == 0 ?
                Utilities.GetLocalizedGametype(s.Gametype) :
                Utilities.GetLocalizedGametype(nextMapMatch.Groups[2].ToString());

            return $"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_NEXTMAP_SUCCESS"]} ^5{nextMap.Alias}/{nextGametype}";
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Origin.Tell(await GetNextMap(E.Owner));
        }
    }
}
