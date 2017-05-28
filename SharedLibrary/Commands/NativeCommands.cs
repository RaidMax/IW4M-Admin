using System;
using System.Collections.Generic;
using System.Text;
using SharedLibrary;
using SharedLibrary.Network;
using System.Threading.Tasks;

namespace SharedLibrary.Commands
{
    class Quit : Command
    {
        public Quit(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

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
            if (E.Owner.Manager.GetClientDatabase().getOwner() == null)
            {
                E.Origin.setLevel(Player.Permission.Owner);
                await E.Origin.Tell("Congratulations, you have claimed ownership of this server!");
                E.Owner.owner = E.Origin;
                E.Owner.Manager.GetClientDatabase().updatePlayer(E.Origin);
            }
            else
                await E.Origin.Tell("This server already has an owner!");
        }
    }

    class Warn : Command
    {
        public Warn(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            E.Target.lastOffense = E.Data.RemoveWords(1);
            if (E.Origin.Level <= E.Target.Level)
                await E.Origin.Tell("You cannot warn " + E.Target.Name);
            else
                await E.Target.Warn(E.Target.lastOffense, E.Origin);   
        }
    }

    class WarnClear : Command
    {
        public WarnClear(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            E.Target.lastOffense = String.Empty;
            E.Target.Warnings = 0;
            String Message = String.Format("All warning cleared for {0}", E.Target.Name);
            await E.Owner.Broadcast(Message);
        }
    }

    class Kick : Command
    {
        public Kick(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            E.Target.lastOffense = E.Data.RemoveWords(1);
            if (E.Origin.Level > E.Target.Level)
                await E.Target.Kick(E.Target.lastOffense, E.Origin);
            else
                await E.Origin.Tell("You cannot kick " + E.Target.Name);            
        }
    }

    class Say : Command
    {
        public Say(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            await E.Owner.Broadcast("^1" + E.Origin.Name + " - ^6" + E.Data + "^7");
        }
    }

    class TempBan : Command
    {
        public TempBan(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            E.Target.lastOffense = SharedLibrary.Utilities.RemoveWords(E.Data, 1);
            String Message = E.Target.lastOffense;
            if (E.Origin.Level > E.Target.Level)
                await E.Target.TempBan(Message, E.Origin);
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
                await E.Origin.Tell(String.Format("Sucessfully banned ^5{0} ^7({1})", E.Target.Name, E.Target.npID));
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
            await E.Origin.Tell($"Successfully unbanned {E.Target.Name}::{E.Target.npID}");
        }
    }

    class CWhoAmI : Command
    {
        public CWhoAmI(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            String You = String.Format("{0} [^3#{1}^7] {2} [^3@{3}^7] [{4}^7] IP: {5}", E.Origin.Name, E.Origin.clientID, E.Origin.npID, E.Origin.databaseID, SharedLibrary.Utilities.levelToColor(E.Origin.Level), E.Origin.IP);
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
                    playerList.AppendFormat("[^3{0}^7]{3}[^3{1}^7] {2}", Utilities.levelToColor(Player.Permission.User), P.clientID, P.Name, SharedLibrary.Utilities.getSpaces(Player.Permission.SeniorAdmin.ToString().Length - Player.Permission.User.ToString().Length));
                else
                    playerList.AppendFormat("[^3{0}^7]{3}[^3{1}^7] {2}", Utilities.levelToColor(P.Level), P.clientID, P.Name, SharedLibrary.Utilities.getSpaces(Player.Permission.SeniorAdmin.ToString().Length - P.Level.ToString().Length));

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
                        if (player != null && player.npID == E.Target.npID)
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
                E.Owner.Manager.GetClientDatabase().updatePlayer(E.Target);
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
                    await E.Origin.Tell(String.Format("[^3{0}^7] {1}", Utilities.levelToColor(P.Level), P.Name));
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
            var db_players = E.Owner.Manager.GetClientDatabase().findPlayers(E.Data.Trim());

            if (db_players == null)
            {
                await E.Origin.Tell("No players found");
                return;
            }

            foreach (Player P in db_players)
            { 
                String mesg = String.Format("[^3{0}^7] [^3@{1}^7] - [{2}^7] - {3} | last seen {4} ago", P.Name, P.databaseID, SharedLibrary.Utilities.levelToColor(P.Level), P.IP, P.getLastConnection());
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

            //var db_players = E.Owner.Manager.GetClientDatabase().findPlayers(E.Data.Trim());
            var db_aliases = E.Owner.aliasDB.findPlayers(E.Data);

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

                Player Current = E.Owner.Manager.GetClientDatabase().getPlayer(P.Number);

                if (Current != null)
                {
                    String mesg = String.Format("^1{0} ^7now goes by ^5{1}^7 [^3{2}^7]", lookingFor, Current.Name, Current.databaseID);
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
                await E.Origin.Tell("The server onwer has not set any rules.");
            else
            {
                foreach (String r in E.Owner.rules)
                    await E.Origin.Tell("- " + r);
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
                await E.Origin.Tell("Sucessfully reloaded configs!");
            else
                await E.Origin.Tell("Unable to reload configs :(");
        }
    }

    class Flag : Command
    {
        public Flag(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

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
                E.Target.setLevel(Player.Permission.Flagged);
                await E.Origin.Tell("You have ^5flagged ^7" + E.Target.Name);
            }

            E.Owner.Manager.GetClientDatabase().updatePlayer(E.Target);
        }
    }

    class CReport : Command
    {
        public CReport(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Owner.Reports.Find(x => (x.Origin == E.Origin && x.Target.npID == E.Target.npID)) != null)
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

            Player Banner = E.Owner.Manager.GetClientDatabase().getPlayer(BannedPenalty.bannedByID, -1);

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
            E.Target.Alias = E.Owner.aliasDB.getPlayer(E.Target.databaseID);

            if (E.Target.Alias == null)
            {
                await E.Target.Tell("Could not find alias info for that player.");
                return;
            }

            await E.Target.Tell("[^3" + E.Target.Name + "^7]");
            StringBuilder message = new StringBuilder();

            List<Player> playerAliases = E.Owner.getPlayerAliases(E.Target);
               
            message.Append("Aliases: ");

            foreach (Player P in playerAliases)
            {
                foreach (String S in P.Alias.Names)
                {
                    if (S != String.Empty && S != E.Target.Name)
                        message.Append(S + "  | ");
                }
            }
            await E.Origin.Tell(message.ToString());

            message = new StringBuilder();

            if (E.Target.Alias.IPS != null)
            {
                message.Append("IPs: ");

                foreach (Player P2 in playerAliases)
                {
                    foreach (String IP in P2.Alias.IPS)
                    {
                        if (IP.Split('.').Length > 3 && IP != String.Empty && !message.ToString().Contains(IP))
                            message.Append (IP + "  | ");
                    }
                }

                await E.Origin.Tell(message.ToString());
            }
        }
    }

    class CExecuteRCON : Command
    {
        public CExecuteRCON(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            await E.Origin.currentServer.ExecuteCommandAsync(E.Data.Trim());
            await E.Origin.Tell("Successfuly sent RCON command!");
        }
    }
}
    