using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
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

namespace SharedLibraryCore.Commands
{
    public class CQuit : Command
    {
        public CQuit() :
            base("quit", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_QUIT_DESC"], "q", EFClient.Permission.Owner, false)
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            return Task.Run(() => { E.Owner.Manager.Stop(); });
        }
    }

    public class COwner : Command
    {
        public COwner() :
            base("owner", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_OWNER_DESC"], "iamgod", EFClient.Permission.User, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (await (E.Owner.Manager.GetClientService() as ClientService).GetOwnerCount() == 0 &&
                !E.Target.SetLevel(EFClient.Permission.Owner, Utilities.IW4MAdminClient(E.Owner)).Failed)
            {
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_OWNER_SUCCESS"]);
            }
            else
            {
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_OWNER_FAIL"]);
            }
        }
    }

    public class CWarn : Command
    {
        public CWarn() :
            base("warn", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_WARN_DESC"], "w", EFClient.Permission.Trusted, true, new CommandArgument[]
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

        public override Task ExecuteAsync(GameEvent E)
        {
            if (E.Target.Warn(E.Data, E.Origin).Failed)
            {
                E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_WARN_FAIL"]} {E.Target.Name}");
            }

            return Task.CompletedTask;
        }
    }

    public class CWarnClear : Command
    {
        public CWarnClear() :
            base("warnclear", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_WARNCLEAR_DESC"], "wc", EFClient.Permission.Trusted, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
                        Required = true
                    }
                })
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            if (!E.Target.WarnClear(E.Origin).Failed)
            {
                E.Owner.Broadcast($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_WARNCLEAR_SUCCESS"]} {E.Target.Name}");
            }

            return Task.CompletedTask;
        }
    }

    public class CKick : Command
    {
        public CKick() :
            base("kick", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_KICK_DESC"], "k", EFClient.Permission.Moderator, true, new CommandArgument[]
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
            var _ = !(await E.Target.Kick(E.Data, E.Origin).WaitAsync()).Failed ?
                  E.Origin.Tell($"^5{E.Target} ^7{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_KICK_SUCCESS"]}") :
                  E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_KICK_FAIL"]} {E.Target.Name}");
        }
    }

    public class CSay : Command
    {
        public CSay() :
            base("say", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SAY_DESC"], "s", EFClient.Permission.Moderator, false, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_MESSAGE"],
                        Required = true
                    }
                })
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            E.Owner.Broadcast($"{(E.Owner.GameName == Server.Game.IW4 ? "^:" : "")}{E.Origin.Name} - ^6{E.Data}^7", E.Origin);
            return Task.CompletedTask;
        }
    }

    public class CTempBan : Command
    {
        public CTempBan() :
            base("tempban", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_TEMPBAN_DESC"], "tb", EFClient.Permission.Administrator, true, new CommandArgument[]
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

        private static readonly string TempBanRegex = @"([0-9]+\w+)\ (.+)";

        public override async Task ExecuteAsync(GameEvent E)
        {
            var match = Regex.Match(E.Data, TempBanRegex);
            if (match.Success)
            {
                string tempbanReason = match.Groups[2].ToString();
                var length = match.Groups[1].ToString().ParseTimespan();

                if (length > E.Owner.Manager.GetApplicationSettings().Configuration().MaximumTempBanTime)
                {
                    E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_TEMPBAN_FAIL_TOOLONG"]);
                }

                else
                {
                    var _ = !(await E.Target.TempBan(tempbanReason, length, E.Origin).WaitAsync()).Failed ?
                        E.Origin.Tell($"^5{E.Target} ^7{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_TEMPBAN_SUCCESS"]} ^5{length.TimeSpanText()}") :
                        E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_TEMPBAN_FAIL"]} {E.Target.Name}");
                }
            }
        }
    }

    public class CBan : Command
    {
        public CBan() :
            base("ban", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_BAN_DESC"], "b", EFClient.Permission.SeniorAdmin, true, new CommandArgument[]
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
            var _ = !(await E.Target.Ban(E.Data, E.Origin, false).WaitAsync()).Failed ?
                    E.Origin.Tell($"^5{E.Target} ^7{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_BAN_SUCCESS"]}") :
                    E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_BAN_FAIL"]} {E.Target.Name}");
        }
    }

    public class CUnban : Command
    {
        public CUnban() :
            base("unban", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_UNBAN_DESC"], "ub", EFClient.Permission.SeniorAdmin, true, new CommandArgument[]
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
                await E.Target.Unban(E.Data, E.Origin).WaitAsync();
                E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_UNBAN_SUCCESS"]} {E.Target}");
            }
            else
            {
                E.Origin.Tell($"{E.Target} {Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_UNBAN_FAIL"]}");
            }
        }
    }

    public class CWhoAmI : Command
    {
        public CWhoAmI() :
            base("whoami", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_WHO_DESC"], "who", EFClient.Permission.User, false)
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            String You = String.Format("{0} [^3#{1}^7] {2} ^7[^3@{3}^7] ^7[{4}^7] IP: {5}", E.Origin.Name, E.Origin.ClientNumber, E.Origin.NetworkId, E.Origin.ClientId, Utilities.ConvertLevelToColor(E.Origin.Level, E.Origin.ClientPermission.Name), E.Origin.IPAddressString);
            E.Origin.Tell(You);

            return Task.CompletedTask;
        }
    }

    public class CList : Command
    {
        public CList() :
            base("list", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_LIST_DESC"], "l", EFClient.Permission.Moderator, false)
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            StringBuilder playerList = new StringBuilder();
            int count = 0;
            for (int i = 0; i < E.Owner.Clients.Count; i++)
            {
                var P = E.Owner.Clients[i];

                if (P == null)
                {
                    continue;
                }
                // todo: fix spacing
                // todo: make this better :)
                if (P.Masked)
                {
                    playerList.AppendFormat("[^3{0}^7]{3}[^3{1}^7] {2}", Utilities.ConvertLevelToColor(EFClient.Permission.User, P.ClientPermission.Name), P.ClientNumber, P.Name, Utilities.GetSpaces(EFClient.Permission.SeniorAdmin.ToString().Length - EFClient.Permission.User.ToString().Length));
                }
                else
                {
                    playerList.AppendFormat("[^3{0}^7]{3}[^3{1}^7] {2}", Utilities.ConvertLevelToColor(P.Level, P.ClientPermission.Name), P.ClientNumber, P.Name, Utilities.GetSpaces(EFClient.Permission.SeniorAdmin.ToString().Length - P.Level.ToString().Length));
                }

                if (count == 2 || E.Owner.GetClientsAsList().Count == 1)
                {
                    E.Origin.Tell(playerList.ToString());
                    count = 0;
                    playerList = new StringBuilder();
                    continue;
                }

                count++;
            }

            if (playerList.Length > 0)
            {
                E.Origin.Tell(playerList.ToString());
            }

            // todo: make no players response for webfront

            return Task.CompletedTask;
        }
    }

    public class CHelp : Command
    {
        public CHelp() :
            base("help", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_HELP_DESC"], "h", EFClient.Permission.User, false, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_COMMANDS"],
                        Required = false
                    }
                })
        { }

        public override Task ExecuteAsync(GameEvent E)
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
                        E.Origin.Tell($"[^3{C.Name}^7] {C.Description}");
                        E.Origin.Tell(C.Syntax);
                        found = true;
                    }
                }

                if (!found)
                {
                    E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_HELP_NOTFOUND"]);
                }
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
                            E.Origin.Tell(helpResponse.ToString());
                            helpResponse = new StringBuilder();
                            count = 0;
                        }
                        count++;
                    }
                }
                E.Origin.Tell(helpResponse.ToString());
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_HELP_MOREINFO"]);
            }

            return Task.CompletedTask;
        }
    }

    public class CFastRestart : Command
    {
        public CFastRestart() :
            base("fastrestart", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FASTRESTART_DESC"], "fr", EFClient.Permission.Moderator, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Owner.ExecuteCommandAsync("fast_restart");

            var _ = !E.Origin.Masked ?
                  E.Owner.Broadcast($"^5{E.Origin.Name} ^7{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FASTRESTART_UNMASKED"]}") :
                 E.Owner.Broadcast(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FASTRESTART_MASKED"]);
        }
    }

    public class CMapRotate : Command
    {
        public CMapRotate() :
            base("maprotate", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MAPROTATE_DESC"], "mr", EFClient.Permission.Administrator, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var _ = !E.Origin.Masked ?
                E.Owner.Broadcast($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MAPROTATE"]} [^5{E.Origin.Name}^7]", E.Origin) :
                E.Owner.Broadcast(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MAPROTATE"], E.Origin);

            await Task.Delay(5000);
            await E.Owner.ExecuteCommandAsync("map_rotate");
        }
    }

    public class CSetLevel : Command
    {
        public CSetLevel() :
            base("setlevel", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_DESC"], "sl", EFClient.Permission.Moderator, true, new CommandArgument[]
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

        public override Task ExecuteAsync(GameEvent E)
        {

            EFClient.Permission oldPerm = E.Target.Level;
            EFClient.Permission newPerm = Utilities.MatchPermission(E.Data);

            if (E.Target == E.Origin)
            {
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_SELF"]);
                return Task.CompletedTask;
            }


            else if (newPerm == EFClient.Permission.Owner &&
                !E.Owner.Manager.GetApplicationSettings().Configuration().EnableMultipleOwners)
            {
                // only one owner is allowed
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_OWNER"]);
                return Task.CompletedTask;
            }

            else if (E.Origin.Level < EFClient.Permission.Owner &&
                !E.Owner.Manager.GetApplicationSettings().Configuration().EnableSteppedHierarchy)
            {
                // only the owner is allowed to set levels
                E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_STEPPEDDISABLED"]} ^5{E.Target.Name}");
                return Task.CompletedTask;
            }

            else if (E.Origin.Level <= newPerm &&
                E.Origin.Level < EFClient.Permission.Owner)
            {
                // can't promote a client to higher than your current perms
                E.Origin.Tell(string.Format(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_LEVELTOOHIGH"], E.Target.Name, (E.Origin.Level - 1).ToString()));
                return Task.CompletedTask;
            }

            else if (newPerm > EFClient.Permission.Banned)
            {
                var ActiveClient = E.Owner.Manager.GetActiveClients()
                    .FirstOrDefault(p => p.NetworkId == E.Target.NetworkId);

                if (ActiveClient != null)
                {
                    ActiveClient.SetLevel(newPerm, E.Origin);

                    // inform the client that they were promoted
                    // we don't really want to tell them if they're demoted haha
                    if (newPerm > oldPerm)
                    {
                        ActiveClient.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_SUCCESS_TARGET"]} {newPerm}");
                    }
                }

                else
                {
                    E.Target.SetLevel(newPerm, E.Origin);
                }

                // inform the origin that the client has been updated
                _ = newPerm < oldPerm ?
                    E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_DEMOTE_SUCCESS"]} {E.Target.Name}") :
                    E.Origin.Tell($"{E.Target.Name} {Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_SUCCESS"]}");
            }

            else
            {
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETLEVEL_FAIL"]);
            }

            return Task.CompletedTask;
        }
    }

    public class CUsage : Command
    {
        public CUsage() :
            base("usage", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_USAGE_DESC"], "us", EFClient.Permission.Moderator, false)
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            E.Origin.Tell($"IW4MAdmin {Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_USAGE_TEXT"]} " + Math.Round(((System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 2048f) / 1200f), 1) + "MB");
            return Task.CompletedTask;
        }
    }

    public class CUptime : Command
    {
        public CUptime() :
            base("uptime", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_UPTIME_DESC"], "up", EFClient.Permission.Moderator, false)
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            TimeSpan uptime = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
            var loc = Utilities.CurrentLocalization.LocalizationIndex;
            E.Origin.Tell($"IW4M Admin {loc["COMMANDS_UPTIME_TEXT"]} {uptime.Days} {loc["GLOBAL_TIME_DAYS"]}, {uptime.Hours} {loc["GLOBAL_TIME_HOURS"]}, {uptime.Minutes} {loc["GLOBAL_TIME_MINUTES"]}");
            return Task.CompletedTask;
        }
    }

    public class CListAdmins : Command
    {
        public CListAdmins() :
            base("admins", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ADMINS_DESC"], "a", EFClient.Permission.User, false)
        { }

        public static string OnlineAdmins(Server S)
        {
            var onlineAdmins = S.GetClientsAsList()
                .Where(p => p.Level > EFClient.Permission.Flagged)
                .Where(p => !p.Masked)
                .Select(p => $"[^3{Utilities.ConvertLevelToColor(p.Level, p.ClientPermission.Name)}^7] {p.Name}");

            return onlineAdmins.Count() > 0 ?
                string.Join(Environment.NewLine, onlineAdmins) :
                Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ADMINS_NONE"];
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            foreach (string line in OnlineAdmins(E.Owner).Split(Environment.NewLine))
            {
                var _ = E.Message.IsBroadcastCommand() ? E.Owner.Broadcast(line) : E.Origin.Tell(line);
            }

            return Task.CompletedTask;
        }
    }

    public class CLoadMap : Command
    {
        public CLoadMap() :
            base("map", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MAP_DESC"], "m", EFClient.Permission.Administrator, false, new CommandArgument[]
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
                    E.Owner.Broadcast($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MAP_SUCCESS"]} ^5{m.Alias}");
                    await Task.Delay(5000);
                    await E.Owner.LoadMap(m.Name);
                    return;
                }
            }

            E.Owner.Broadcast($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MAP_UKN"]} ^5{newMap}");
            await Task.Delay(5000);
            await E.Owner.LoadMap(newMap);
        }
    }

    public class CFindPlayer : Command
    {
        public CFindPlayer() :
            base("find", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FIND_DESC"], "f", EFClient.Permission.Administrator, false, new CommandArgument[]
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
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FIND_MIN"]);
                return;
            }

            IList<EFClient> db_players = (await (E.Owner.Manager.GetClientService() as ClientService)
                .FindClientsByIdentifier(E.Data))
                .OrderByDescending(p => p.LastConnection)
                .ToList();

            if (db_players.Count == 0)
            {
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FIND_EMPTY"]);
                return;
            }

            foreach (var P in db_players)
            {
                string localizedLevel = Utilities.CurrentLocalization.LocalizationIndex[$"GLOBAL_PERMISSION_{P.Level.ToString().ToUpper()}"];
                // they're not going by another alias
                string msg = P.Name.ToLower().Contains(E.Data.ToLower()) ?
                    $"[^3{P.Name}^7] [^3@{P.ClientId}^7] - [{ Utilities.ConvertLevelToColor(P.Level, localizedLevel)}^7] - {P.IPAddressString} | last seen {Utilities.GetTimePassed(P.LastConnection)}" :
                    $"({P.AliasLink.Children.FirstOrDefault(a => a.Name.ToLower().Contains(E.Data.ToLower()))?.Name})->[^3{P.Name}^7] [^3@{P.ClientId}^7] - [{ Utilities.ConvertLevelToColor(P.Level, localizedLevel)}^7] - {P.IPAddressString} | last seen {Utilities.GetTimePassed(P.LastConnection)}";
                E.Origin.Tell(msg);
            }
        }
    }

    public class CListRules : Command
    {
        public CListRules() :
            base("rules", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_RULES_DESC"], "r", EFClient.Permission.User, false)
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            if (E.Owner.Manager.GetApplicationSettings().Configuration().GlobalRules?.Count < 1 &&
                E.Owner.ServerConfig.Rules?.Count < 1)
            {
                var _ = E.Message.IsBroadcastCommand() ?
                      E.Owner.Broadcast(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_RULES_NONE"]) :
                      E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_RULES_NONE"]);
            }

            else
            {
                var rules = new List<string>();
                rules.AddRange(E.Owner.Manager.GetApplicationSettings().Configuration().GlobalRules);
                if (E.Owner.ServerConfig.Rules != null)
                {
                    rules.AddRange(E.Owner.ServerConfig.Rules);
                }

                foreach (string r in rules)
                {
                    var _ = E.Message.IsBroadcastCommand() ? E.Owner.Broadcast($"- {r}") : E.Origin.Tell($"- {r}");
                }
            }

            return Task.CompletedTask;
        }
    }

    public class CPrivateMessage : Command
    {
        public CPrivateMessage() :
            base("privatemessage", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PM_DESC"], "pm", EFClient.Permission.User, true, new CommandArgument[]
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

        public override Task ExecuteAsync(GameEvent E)
        {
            E.Target.Tell($"^1{E.Origin.Name} ^3[PM]^7 - {E.Data}");
            E.Origin.Tell($"To ^3{E.Target.Name} ^7-> {E.Data}");
            return Task.CompletedTask;
        }
    }


    public class CFlag : Command
    {
        public CFlag() :
            base("flag", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FLAG_DESC"], "fp", EFClient.Permission.Moderator, true, new CommandArgument[]
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

        public override Task ExecuteAsync(GameEvent E)
        {
            var flagEvent = E.Target.Flag(E.Data, E.Origin);

            if (E.FailReason == GameEvent.EventFailReason.Permission)
            {
                E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FLAG_FAIL"]} ^5{E.Target.Name}");
            }

            else if (E.FailReason == GameEvent.EventFailReason.Invalid)
            {
                E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FLAG_ALREADYFLAGGED"]}");
            }

            else
            {
                E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FLAG_SUCCESS"]} ^5{E.Target.Name}");
            }

            return Task.CompletedTask;

        }
    }

    public class CUnflag : Command
    {
        public CUnflag() :
            base("unflag", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_UNFLAG_DESC"], "uf", EFClient.Permission.Moderator, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
                        Required = true
                    }
                })
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            var unflagEvent = E.Target.Unflag(E.Data, E.Origin);

            if (unflagEvent.FailReason == GameEvent.EventFailReason.Permission)
            {
                E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_UNFLAG_FAIL"]} ^5{E.Target.Name}");
            }

            else if (unflagEvent.FailReason == GameEvent.EventFailReason.Invalid)
            {
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_UNFLAG_NOTFLAGGED"]);
            }

            else
            {
                E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_FLAG_UNFLAG"]} ^5{E.Target.Name}");
            }

            return Task.CompletedTask;
            // todo: update immediately?
        }
    }


    public class CReport : Command
    {
        public CReport() :
            base("report", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORT_DESC"], "rep", EFClient.Permission.User, true, new CommandArgument[]
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

        public override async Task ExecuteAsync(GameEvent commandEvent)
        {
            if (commandEvent.Data.ToLower().Contains("camp"))
            {
                commandEvent.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORT_FAIL_CAMP"]);
                return;
            }

            var reportEvent = commandEvent.Target.Report(commandEvent.Data, commandEvent.Origin);

            if (reportEvent.FailReason == GameEvent.EventFailReason.Permission)
            {
                commandEvent.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORT_FAIL"]} {commandEvent.Target.Name}");
            }

            else if (reportEvent.FailReason == GameEvent.EventFailReason.Invalid)
            {
                commandEvent.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORT_FAIL_SELF"]);
            }

            else if (reportEvent.FailReason == GameEvent.EventFailReason.Throttle)
            {
                commandEvent.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORT_FAIL_TOOMANY"]);
            }

            else if (reportEvent.Failed)
            {
                commandEvent.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORT_FAIL_DUPLICATE"]);
            }

            else
            {
                // todo: move into server
                Penalty newReport = new Penalty()
                {
                    Type = Penalty.PenaltyType.Report,
                    Expires = DateTime.UtcNow,
                    Offender = commandEvent.Target,
                    Offense = commandEvent.Data,
                    Punisher = commandEvent.Origin,
                    Active = true,
                    When = DateTime.UtcNow,
                    Link = commandEvent.Target.AliasLink
                };

                await commandEvent.Owner.Manager.GetPenaltyService().Create(newReport);

                commandEvent.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORT_SUCCESS"]);
                commandEvent.Owner.ToAdmins(String.Format("^5{0}^7->^1{1}^7: {2}", commandEvent.Origin.Name, commandEvent.Target.Name, commandEvent.Data));
            }
        }
    }

    public class CListReports : Command
    {
        public CListReports() :
            base("reports", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORTS_DESC"], "reps", EFClient.Permission.Moderator, false, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_CLEAR"],
                        Required = false
                    }
                })
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            if (E.Data != null && E.Data.ToLower().Contains(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_CLEAR"]))
            {
                E.Owner.Reports = new List<Report>();
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORTS_CLEAR_SUCCESS"]);
                return Task.CompletedTask;
            }

            if (E.Owner.Reports.Count < 1)
            {
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_REPORTS_NONE"]);
                return Task.CompletedTask;
            }

            foreach (Report R in E.Owner.Reports)
            {
                E.Origin.Tell(String.Format("^5{0}^7->^1{1}^7: {2}", R.Origin.Name, R.Target.Name, R.Reason));
            }

            return Task.CompletedTask;
        }
    }

    public class CMask : Command
    {
        public CMask() :
            base("mask", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MASK_DESC"], "hide", EFClient.Permission.Moderator, false)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Origin.Masked)
            {
                E.Origin.Masked = false;
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MASK_OFF"]);
            }
            else
            {
                E.Origin.Masked = true;
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_MASK_ON"]);
            }

            await E.Owner.Manager.GetClientService().Update(E.Origin);
        }
    }

    public class CListBanInfo : Command
    {
        public CListBanInfo() :
            base("baninfo", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_BANINFO_DESC"], "bi", EFClient.Permission.Moderator, true, new CommandArgument[]
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

            var penalty = B.FirstOrDefault(b => b.Type > Penalty.PenaltyType.Kick &&
                (b.Expires == null || b.Expires > DateTime.UtcNow));

            if (penalty == null)
            {
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_BANINFO_NONE"]);
                return;
            }

            string timeRemaining = penalty.Type == Penalty.PenaltyType.TempBan ? $"({(penalty.Expires.Value - DateTime.UtcNow).TimeSpanText()} remaining)" : "";
            string success = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_BANINFO_SUCCESS"];

            E.Origin.Tell($"^1{E.Target.Name} ^7{string.Format(success, penalty.Punisher.Name)} {penalty.Punisher.Name} {timeRemaining}");
        }
    }

    public class CListAlias : Command
    {
        public CListAlias() :
            base("alias", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ALIAS_DESC"], "known", EFClient.Permission.Moderator, true, new CommandArgument[]
                {
                    new CommandArgument()
                    {
                        Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
                        Required = true,
                    }
                })
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            StringBuilder message = new StringBuilder();
            var names = new List<string>(E.Target.AliasLink.Children.Select(a => a.Name));
            var IPs = new List<string>(E.Target.AliasLink.Children.Select(a => a.IPAddress.ConvertIPtoString()).Distinct());

            E.Target.Tell($"[^3{E.Target}^7]");

            message.Append($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ALIAS_ALIASES"]}: ");
            message.Append(String.Join(" | ", names));
            E.Origin.Tell(message.ToString());

            message.Clear();
            message.Append($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ALIAS_IPS"]}: ");
            message.Append(String.Join(" | ", IPs));
            E.Origin.Tell(message.ToString());

            return Task.CompletedTask;
        }
    }

    public class CExecuteRCON : Command
    {
        public CExecuteRCON() :
            base("rcon", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_RCON_DESC"], "rcon", EFClient.Permission.Owner, false, new CommandArgument[]
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
            {
                E.Origin.Tell(S.StripColors());
            }

            if (Response.Length == 0)
            {
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_RCON_SUCCESS"]);
            }
        }
    }

    public class CPlugins : Command
    {
        public CPlugins() :
            base("plugins", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PLUGINS_DESC"], "p", EFClient.Permission.Administrator, false)
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PLUGINS_LOADED"]);
            foreach (var P in Plugins.PluginImporter.ActivePlugins)
            {
                E.Origin.Tell(String.Format("^3{0} ^7[v^3{1}^7] by ^5{2}^7", P.Name, P.Version, P.Author));
            }
            return Task.CompletedTask;
        }
    }

    public class CIP : Command
    {
        public CIP() :
            base("getexternalip", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_IP_DESC"], "ip", EFClient.Permission.User, false)
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_IP_SUCCESS"]} ^5{E.Origin.IPAddressString}");
            return Task.CompletedTask;
        }
    }

    public class CPruneAdmins : Command
    {
        public CPruneAdmins() : base("prune", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PRUNE_DESC"], "pa", EFClient.Permission.Owner, false, new CommandArgument[]
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
                    {
                        throw new FormatException();
                    }
                }
            }

            catch (FormatException)
            {
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PRUNE_FAIL"]);
                return;
            }

            List<EFClient> inactiveUsers = null;
            // todo: make an event for this
            // update user roles
            using (var context = new DatabaseContext())
            {
                var lastActive = DateTime.UtcNow.AddDays(-inactiveDays);
                inactiveUsers = await context.Clients
                    .Where(c => c.Level > EFClient.Permission.Flagged && c.Level <= EFClient.Permission.Moderator)
                    .Where(c => c.LastConnection < lastActive)
                    .ToListAsync();
                inactiveUsers.ForEach(c => c.Level = EFClient.Permission.User);
                await context.SaveChangesAsync();
            }
            E.Origin.Tell($"^5{inactiveUsers.Count} ^7{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PRUNE_SUCCESS"]}");
        }
    }

    public class CSetPassword : Command
    {
        public CSetPassword() : base("setpassword", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_SETPASSWORD_DESC"], "sp", EFClient.Permission.Moderator, false, new CommandArgument[]
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
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PASSWORD_FAIL"]);
                return;
            }

            string[] hashedPassword = Helpers.Hashing.Hash(E.Data);

            E.Origin.Password = hashedPassword[0];
            E.Origin.PasswordSalt = hashedPassword[1];

            // update the password for the client in privileged
            E.Owner.Manager.GetPrivilegedClients()[E.Origin.ClientId].Password = hashedPassword[0];
            E.Owner.Manager.GetPrivilegedClients()[E.Origin.ClientId].PasswordSalt = hashedPassword[1];

            await E.Owner.Manager.GetClientService().Update(E.Origin);
            E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PASSWORD_SUCCESS"]);
        }
    }

    public class CKillServer : Command
    {
        public CKillServer() : base("killserver", "kill the game server", "kill", EFClient.Permission.Administrator, false)
        {
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Owner.ServerConfig.ManualLogPath != null)
            {
                using (var wc = new WebClient())
                {
                    E.Owner.RestartRequested = true;
                    var response = await wc.DownloadStringTaskAsync(new Uri($"{E.Owner.ServerConfig.ManualLogPath}/restart"));
                }
            }

            else
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
                    E.Origin.Tell("Could not find running/stalled instance of IW4x");
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
                        E.Origin.Tell("Unable to cleanly shutdown server, forcing");
                    }

                    if (!currentProcess.HasExited)
                    {
                        try
                        {
                            currentProcess.Kill();
                            E.Origin.Tell("Successfully killed server process");
                        }
                        catch (Exception e)
                        {
                            E.Origin.Tell("Could not kill server process");
                            E.Owner.Logger.WriteDebug("Unable to kill process");
                            E.Owner.Logger.WriteDebug($"Exception: {e.Message}");
                            return;
                        }
                    }
                }

                return;
            }
        }
    }


    public class CPing : Command
    {
        public CPing() : base("ping", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PING_DESC"], "pi", EFClient.Permission.User, false, new CommandArgument[]
        {
            new CommandArgument()
            {
                Name = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_ARGS_PLAYER"],
                Required = false
            }
        })
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            if (E.Message.IsBroadcastCommand())
            {
                if (E.Target == null)
                {
                    E.Owner.Broadcast($"{E.Origin.Name}'s {Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PING_TARGET"]} ^5{E.Origin.Ping}^7ms");
                }
                else
                {
                    E.Owner.Broadcast($"{E.Target.Name}'s {Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PING_TARGET"]} ^5{E.Target.Ping}^7ms");
                }
            }
            else
            {
                if (E.Target == null)
                {
                    E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PING_SELF"]} ^5{E.Origin.Ping}^7ms");
                }
                else
                {
                    E.Origin.Tell($"{E.Target.Name}'s {Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_PING_TARGET"]} ^5{E.Target.Ping}^7ms");
                }
            }

            return Task.CompletedTask;
        }
    }

    public class CSetGravatar : Command
    {
        public CSetGravatar() : base("setgravatar", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_GRAVATAR_DESC"], "sg", EFClient.Permission.User, false, new CommandArgument[]
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
                        E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_GRAVATAR_SUCCESS_NEW"]);
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
                    E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_GRAVATAR_SUCCESS_UPDATE"]);
                }
            }
        }
    }

    /// <summary>
    /// Retrieves the next map in rotation
    /// </summary>
    public class CNextMap : Command
    {
        public CNextMap() : base("nextmap", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_NEXTMAP_DESC"], "nm", EFClient.Permission.User, false) { }
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
            E.Origin.Tell(await GetNextMap(E.Owner));
        }
    }
}