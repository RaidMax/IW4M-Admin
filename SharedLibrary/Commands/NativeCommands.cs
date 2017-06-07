using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using SharedLibrary;
using SharedLibrary.Network;
using System.Threading.Tasks;

namespace SharedLibrary.Commands
{
    class CQuit : Command
    {
        public CQuit(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            E.Owner.Manager.Stop();
        }
    }

    class Owner : Command
    {
        
        public Owner(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Owner.Manager.GetClientDatabase().GetOwner() == null)
            {
                E.Origin.setLevel(Player.Permission.Owner);
                await E.Origin.Tell("Congratulations, you have claimed ownership of this server!");
                E.Owner.owner = E.Origin;
                E.Owner.Manager.GetClientDatabase().UpdatePlayer(E.Origin);
            }
            else
                await E.Origin.Tell("This server already has an owner!");
        }
    }

    class Cwarn : Command
    {
        public Cwarn(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            E.Target.lastOffense = E.Data.RemoveWords(1);
            if (E.Origin.Level <= E.Target.Level)
                await E.Origin.Tell("You cannot warn " + E.Target.Name);
            else
                await E.Target.Warn(E.Target.lastOffense, E.Origin);   
        }
    }

    class CWarnClear : Command
    {
        public CWarnClear(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            E.Target.lastOffense = String.Empty;
            E.Target.Warnings = 0;
            String Message = String.Format("All warning cleared for {0}", E.Target.Name);
            await E.Owner.Broadcast(Message);
        }
    }

    class CKick : Command
    {
        public CKick(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            E.Target.lastOffense = E.Data.RemoveWords(1);
            if (E.Origin.Level > E.Target.Level)
                await E.Target.Kick(E.Target.lastOffense, E.Origin);
            else
                await E.Origin.Tell("You cannot kick " + E.Target.Name);            
        }
    }

    class CSay : Command
    {
        public CSay(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            await E.Owner.Broadcast("^1" + E.Origin.Name + " - ^6" + E.Data + "^7");
        }
    }

    class CTempBan : Command
    {
        public CTempBan(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            E.Target.lastOffense = SharedLibrary.Utilities.RemoveWords(E.Data, 1);
            String Message = E.Target.lastOffense;
            if (E.Origin.Level > E.Target.Level)
            {
                await E.Target.TempBan(Message, E.Origin);
                await E.Origin.Tell($"Successfully temp banned {E.Target.Name}");
            }
            else
                await E.Origin.Tell("You cannot temp ban " + E.Target.Name);
        }
    }

    class CBan : Command
    {
        public CBan(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            E.Target.lastOffense = SharedLibrary.Utilities.RemoveWords(E.Data, 1);
            E.Target.lastEvent = E; // needs to be fixed
            String Message;
            if (E.Owner.Website == null)
                Message = "^1Player Banned: ^5" + E.Target.lastOffense;
            else
                Message = "^1Player Banned: ^5" + E.Target.lastOffense;
            if (E.Origin.Level > E.Target.Level)
            {
                await E.Target.Ban(Message, E.Origin);
                await E.Origin.Tell(String.Format("Sucessfully banned ^5{0} ^7({1})", E.Target.Name, E.Target.NetworkID));
            }
            else
                await E.Origin.Tell("You cannot ban " + E.Target.Name);
        }
    }

    class CUnban : Command
    {
        public CUnban(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            await E.Owner.Unban(E.Target);
            await E.Origin.Tell($"Successfully unbanned {E.Target.Name}::{E.Target.NetworkID}");
        }
    }

    class CWhoAmI : Command
    {
        public CWhoAmI(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            String You = String.Format("{0} [^3#{1}^7] {2} [^3@{3}^7] [{4}^7] IP: {5}", E.Origin.Name, E.Origin.ClientID, E.Origin.NetworkID, E.Origin.DatabaseID, SharedLibrary.Utilities.levelToColor(E.Origin.Level), E.Origin.IP);
            await E.Origin.Tell(You);
        }
    }

    class CList : Command
    {
        public CList(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

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
                    playerList.AppendFormat("[^3{0}^7]{3}[^3{1}^7] {2}", Utilities.levelToColor(Player.Permission.User), P.ClientID, P.Name, SharedLibrary.Utilities.getSpaces(Player.Permission.SeniorAdmin.ToString().Length - Player.Permission.User.ToString().Length));
                else
                    playerList.AppendFormat("[^3{0}^7]{3}[^3{1}^7] {2}", Utilities.levelToColor(P.Level), P.ClientID, P.Name, SharedLibrary.Utilities.getSpaces(Player.Permission.SeniorAdmin.ToString().Length - P.Level.ToString().Length));

                if (count == 2 || E.Owner.getPlayers().Count == 1)
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

    class CHelp : Command
    {
        public CHelp(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            String cmd = E.Data.Trim();

            if (cmd.Length > 2)
            {
                bool found = false;
                foreach (Command C in E.Owner.Manager.GetCommands())
                {
                    if (C.Name.Contains(cmd) || C.Name == cmd)
                    {
                        await E.Origin.Tell(" [^3" + C.Name + "^7] " + C.Description);
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
                List<Command> CommandList = E.Owner.Manager.GetCommands();

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
                await E.Origin.Tell("Type !help <cmd> to get command usage example");
            }
        }
    }

    class CFastRestart : Command
    {
        public CFastRestart(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            if (!E.Origin.Masked)
                await E.Owner.Broadcast($"Fast restarting in ^53 ^7seconds [^5{E.Origin.Name}^7]");
            else
                await E.Owner.Broadcast($"Fast restarting in ^53 ^7seconds [^5Masked Admin^7]");
            await Task.Delay(3000);
            await E.Owner.ExecuteCommandAsync("fast_restart");
        }
    }

    class CMapRotate : Command
    {
        public CMapRotate(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            if (!E.Origin.Masked)
                await E.Owner.Broadcast($"Map rotating in ^55 ^7seconds [^5{E.Origin.Name}^7]");
            else
                await E.Owner.Broadcast($"Map rotating in ^55 ^7seconds [^5Masked Admin^7]");
            await Task.Delay(5000);
            await E.Owner.ExecuteCommandAsync("map_rotate");
        }
    }

    class CSetLevel : Command
    {
        public CSetLevel(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Target == E.Origin)
            {
                await E.Origin.Tell("You cannot change your own level.");
                return;
            }

            Player.Permission newPerm = Utilities.matchPermission(Utilities.RemoveWords(E.Data, 1));

            if (newPerm == Player.Permission.Owner && E.Origin.Level != Player.Permission.Console)
                newPerm = Player.Permission.Banned;

            bool playerInOtherServer = false;

            if (newPerm > Player.Permission.Banned)
            {
                E.Target.setLevel(newPerm);
                // prevent saving of old permissions on disconnect
                // todo: manager DB
                foreach (var server in E.Owner.Manager.GetServers())
                {
                    foreach (var player in server.getPlayers())
                    {
                        if (player != null && player.NetworkID == E.Target.NetworkID)
                        {
                            player.setLevel(newPerm);
                            await E.Target.Tell("Congratulations! You have been promoted to ^3" + newPerm);
                            playerInOtherServer = true;
                        }
                    }
                }

                if (!playerInOtherServer)
                    await E.Target.Tell("Congratulations! You have been promoted to ^3" + newPerm);
                await E.Origin.Tell(E.Target.Name + " was successfully promoted!");
           
                //NEEED TO MOVE
                E.Owner.Manager.GetClientDatabase().UpdatePlayer(E.Target);
            }

            else
                await E.Origin.Tell("Invalid group specified.");
        }
    }

    class CUsage : Command
    {
        public CUsage(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            await E.Origin.Tell("IW4M Admin is using " + Math.Round(((System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 2048f) / 1200f), 1) + "MB");
        }
    }

    class CUptime : Command
    {
        public CUptime(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            TimeSpan uptime = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
            await E.Origin.Tell(String.Format("IW4M Admin has been up for {0} days, {1} hours, and {2} minutes", uptime.Days, uptime.Hours, uptime.Minutes));
        }
    }

    class CListAdmins : Command
    {
        public CListAdmins(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            for (int i = 0; i < E.Owner.Players.Count; i++)
            {
                var P = E.Owner.Players[i];
                if (P != null && P.Level > Player.Permission.Flagged && !P.Masked)
                {
                    if (E.Message[0] == '@')
                        await E.Owner.Broadcast(String.Format("[^3{0}^7] {1}", Utilities.levelToColor(P.Level), P.Name));
                    else
                        await E.Origin.Tell(String.Format("[^3{0}^7] {1}", Utilities.levelToColor(P.Level), P.Name));
                }
            }
        }
    }

    class CLoadMap : Command
    {
        public CLoadMap(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            string newMap = E.Data.Trim().ToLower();
            foreach (Map m in E.Owner.maps)
            {
                if (m.Name.ToLower() == newMap || m.Alias.ToLower() == newMap)
                {
                    await E.Owner.Broadcast("Changing to map ^2" + m.Alias);
                    await Task.Delay(5000);
                    await E.Owner.LoadMap(m.Name);
                    return;
                }
            }

            await E.Owner.Broadcast("Attempting to change to unknown map ^1" + newMap);
            await Task.Delay(5000);
            await E.Owner.LoadMap(newMap);
        }
    }

    class CFindPlayer : Command
    {
        public CFindPlayer(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            var db_players = E.Owner.Manager.GetClientDatabase().FindPlayers(E.Data.Trim());

            if (db_players == null)
            {
                await E.Origin.Tell("No players found");
                return;
            }

            foreach (Player P in db_players)
            { 
                String mesg = String.Format("[^3{0}^7] [^3@{1}^7] - [{2}^7] - {3} | last seen {4} ago", P.Name, P.DatabaseID, SharedLibrary.Utilities.levelToColor(P.Level), P.IP, P.getLastConnection());
                await E.Origin.Tell(mesg);
            }
        }
    }

    class CFindAllPlayers : Command
    {
        public CFindAllPlayers(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            E.Data = E.Data.Trim();

            if (E.Data.Length < 4)
            {
                await E.Origin.Tell("You must enter at least 4 letters");
                return;
            }

            var db_aliases = E.Owner.Manager.GetAliasesDatabase().FindPlayerAliases(E.Data);

            if (db_aliases == null)
            {
                await E.Origin.Tell("No players found");
                return;
            }

            foreach (Aliases P in db_aliases)
            {
                if (P == null)
                    continue;

                String lookingFor = String.Empty;

                foreach(String S in P.Names)
                {
                    if (S.Contains(E.Data))
                        lookingFor = S;
                }

                Player Current = E.Owner.Manager.GetClientDatabase().GetPlayer(P.Number);

                if (Current != null)
                {
                    String mesg = String.Format("^1{0} ^7now goes by ^5{1}^7 [^3{2}^7]", lookingFor, Current.Name, Current.DatabaseID);
                    await E.Origin.Tell(mesg);
                }
            }
        }
    }

    class CListRules : Command
    {
        public CListRules(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Owner.rules.Count < 1)
            {
                if (E.Message.IsBroadcastCommand())
                    await E.Owner.Broadcast("The server owner has not set any rules.");
                else
                    await E.Origin.Tell("The server owner has not set any rules.");
            }

            else
            {
                foreach (String r in E.Owner.rules)
                {
                    if (E.Message.IsBroadcastCommand())
                        await E.Owner.Broadcast("- " + r);
                    else
                        await E.Origin.Tell("- " + r);
                }
            }
        }
    }

    class CPrivateMessage : Command
    {
        public CPrivateMessage(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            E.Data = Utilities.RemoveWords(E.Data, 1);
            await E.Target.Tell("^1" + E.Origin.Name + " ^3[PM]^7 - " + E.Data);
            await E.Origin.Tell(String.Format("To ^3{0} ^7-> {1}", E.Target.Name, E.Data));
        }
    }

    class CReload : Command
    {
        public CReload(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        { 
            if (E.Owner.Reload())
                await E.Origin.Tell("Sucessfully reloaded configuration files");
            else
                await E.Origin.Tell("Unable to reload configuration files");
        }
    }

    class CFlag : Command
    {
        public CFlag(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Target.Level >= E.Origin.Level)
            {
                await E.Origin.Tell("You cannot flag " + E.Target.Name);
                return;
            }

            if (E.Target.Level == Player.Permission.Flagged)
            {
                E.Target.setLevel(Player.Permission.User);
                await E.Origin.Tell("You have ^5unflagged ^7" + E.Target.Name);
            }

            else
            {
                E.Data = Utilities.RemoveWords(E.Data, 1);
                E.Target.setLevel(Player.Permission.Flagged);
                E.Owner.Manager.GetClientPenalties().AddPenalty(new Penalty(Penalty.Type.Flag, E.Data, E.Target.NetworkID, E.Origin.NetworkID, DateTime.Now, E.Target.IP));
                await E.Origin.Tell("You have ^5flagged ^7" + E.Target.Name);
            }

            E.Owner.Manager.GetClientDatabase().UpdatePlayer(E.Target);
        }
    }

    class CReport : Command
    {
        public CReport(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Owner.Reports.Find(x => (x.Origin == E.Origin && x.Target.NetworkID == E.Target.NetworkID)) != null)
            {
                await E.Origin.Tell("You have already reported this player");
                return;
            }

            if (E.Target == E.Origin)
            {
                await E.Origin.Tell("You cannot report yourself.");
                return;
            }

            if (E.Target.Level > E.Origin.Level)
            {
                await E.Origin.Tell("You cannot report " + E.Target.Name);
                return;
            }

            E.Data = E.Data.RemoveWords(1);
            E.Owner.Reports.Add(new Report(E.Target, E.Origin, E.Data));

            await E.Origin.Tell("Successfully reported " + E.Target.Name);
            await E.Owner.ExecuteEvent(new Event(Event.GType.Report, E.Data, E.Origin, E.Target, E.Owner));

            await E.Owner.ToAdmins(String.Format("^5{0}^7->^1{1}^7: {2}", E.Origin.Name, E.Target.Name, E.Data));
        }
    }

    class CListReports : Command
    {
        public CListReports(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

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
                await E.Origin.Tell("No players reported yet.");
                return;
            }

            foreach (Report R in E.Owner.Reports)
                await E.Origin.Tell(String.Format("^5{0}^7->^1{1}^7: {2}", R.Origin.Name, R.Target.Name, R.Reason));
        }
    }

    class CMask : Command
    {
        public CMask(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

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
        }
    }

    class CListBanInfo : Command
    {
        public CListBanInfo(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Target == null)
            {
                await E.Origin.Tell("No bans for that player.");
                return;
            }

            var B = E.Owner.Manager.GetClientPenalties().FindPenalties(E.Target);
            var BannedPenalty = B.Find(b => b.BType == Penalty.Type.Ban);


            if (BannedPenalty == null)
            {
                await E.Origin.Tell("No active ban was found for that player.");
                return;
            }

            Player Banner = E.Owner.Manager.GetClientDatabase().GetPlayer(BannedPenalty.PenaltyOriginID, -1);

            if (Banner == null)
            {
                await E.Origin.Tell("Ban was found for the player, but origin of the ban is unavailable.");
                return;
            }

            await E.Origin.Tell(String.Format("^1{0} ^7was banned by ^5{1} ^7for: {2}", E.Target.Name, Banner.Name, BannedPenalty.Reason));
        }
    }

    class CListAlias : Command
    {
        public CListAlias(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            E.Target.Alias = E.Owner.Manager.GetAliasesDatabase().GetPlayerAliases(E.Target.DatabaseID);

            if (E.Target.Alias == null)
            {
                await E.Target.Tell("Could not find alias info for that player.");
                return;
            }

            await E.Target.Tell("[^3" + E.Target.Name + "^7]");
            StringBuilder message = new StringBuilder();

            var playerAliases = E.Owner.GetAliases(E.Target);
               
            message.Append("Aliases: ");

            var names = new List<string>();
            var ips = new List<string>();

            foreach (var alias in playerAliases)
            {
                names.AddRange(alias.Names);
                ips.AddRange(alias.IPS);
            }
            message.Append(String.Join(" | ", names.Distinct()));

            await E.Origin.Tell(message.ToString());

            message.Clear();
            message.Append("IPs: ");
            message.Append(String.Join(" | ", ips.Distinct()));

            await E.Origin.Tell(message.ToString());
        }
    }

    class CExecuteRCON : Command
    {
        public CExecuteRCON(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            var Response = await E.Owner.ExecuteCommandAsync(E.Data.Trim());
            foreach (string S in Response)
                await E.Origin.Tell(S.StripColors());
            if (Response.Length == 0)
                await E.Origin.Tell("Successfully sent RCON command!");
        }
    }
}
    