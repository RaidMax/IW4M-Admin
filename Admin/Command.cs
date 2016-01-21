using System;
using System.Collections.Generic;
using System.Text;
using SharedLibrary;

namespace IW4MAdmin
{
    class Owner : Command
    {
        public Owner(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            if (E.Owner.clientDB.getOwner() == null)
            {
                E.Origin.setLevel(Player.Permission.Owner);
                E.Origin.Tell("Congratulations, you have claimed ownership of this server!");
                E.Owner.owner = E.Origin;
                E.Owner.clientDB.updatePlayer(E.Origin);
            }
            else
                E.Origin.Tell("This server already has an owner!");
        }
    }

    class Warn : Command
    {
        public Warn(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Target.lastOffense = SharedLibrary.Utilities.removeWords(E.Data, 1);
            if (E.Origin.Level <= E.Target.Level)
                E.Origin.Tell("You cannot warn " + E.Target.Name);
            else
            {
                E.Target.Warn(E.Target.lastOffense, E.Origin);
            }       
        }
    }

    class WarnClear : Command
    {
        public WarnClear(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Target.lastOffense = String.Empty;
            E.Target.Warnings = 0;
            String Message = String.Format("All warning cleared for {0}", E.Target.Name);
            E.Owner.Broadcast(Message);
        }
    }

    class Kick : Command
    {
        public Kick(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Target.lastOffense = SharedLibrary.Utilities.removeWords(E.Data, 1);
            if (E.Origin.Level > E.Target.Level)
                E.Target.Kick(E.Target.lastOffense, E.Origin);
            else
                E.Origin.Tell("You cannot kick " + E.Target.Name);            
        }
    }

    class Say : Command
    {
        public Say(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Owner.Broadcast("^1" + E.Origin.Name + " - ^6" + E.Data + "^7");
        }
    }

    class TempBan : Command
    {
        public TempBan(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Target.lastOffense = SharedLibrary.Utilities.removeWords(E.Data, 1);
            String Message = "^1Player Temporarily Banned: ^5" + E.Target.lastOffense + "^7 (1 hour)";
            if (E.Origin.Level > E.Target.Level)
                E.Target.tempBan(Message, E.Origin);
            else
                E.Origin.Tell("You cannot temp ban " + E.Target.Name);
        }
    }

    class SBan : Command
    {
        public SBan(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Target.lastOffense = SharedLibrary.Utilities.removeWords(E.Data, 1);
            E.Target.lastEvent = E; // needs to be fixed
            String Message;
            if (E.Owner.Website == null)
                Message = "^1Player Banned: ^5" + E.Target.lastOffense;
            else
                Message = "^1Player Banned: ^5" + E.Target.lastOffense + "^7 (appeal at" + E.Owner.Website + ")";
            if (E.Origin.Level > E.Target.Level)
            {
                E.Target.Ban(Message, E.Origin);
                E.Origin.Tell(String.Format("Sucessfully banned ^5{0} ^7({1})", E.Target.Name, E.Target.npID));
            }
            else
                E.Origin.Tell("You cannot ban " + E.Target.Name);
        }
    }

    class Unban : Command
    {
        public Unban(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            if (E.Owner.Unban(E.Data.Trim(), E.Target))
                E.Origin.Tell("Successfully unbanned " + E.Target.Name);
            else
                E.Origin.Tell("Unable to find a ban for that GUID");
        }
    }

    class WhoAmI : Command
    {
        public WhoAmI(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            String You = String.Format("{0} [^3#{1}^7] {2} [^3@{3}^7] [{4}^7] IP: {5}", E.Origin.Name, E.Origin.clientID, E.Origin.npID, E.Origin.databaseID, SharedLibrary.Utilities.levelToColor(E.Origin.Level), E.Origin.IP);
            E.Origin.Tell(You);
        }
    }

    class List : Command
    {
        public List(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            StringBuilder playerList = new StringBuilder();
            lock (E.Owner.getPlayers())
            {
                int count = 0;
                foreach (Player P in E.Owner.getPlayers())
                {
                    if (P == null)
                        continue;

                    playerList.AppendFormat("[^3{0}^7]{3}[^3{1}^7] {2}", SharedLibrary.Utilities.levelToColor(P.Level), P.clientID, P.Name, SharedLibrary.Utilities.getSpaces(Player.Permission.SeniorAdmin.ToString().Length - P.Level.ToString().Length));
                    if (count == 2)
                    {
                        E.Origin.Tell(playerList.ToString());
                        count = 0;
                        playerList = new StringBuilder();
                        continue;
                    }

                    count++;
                }
            }
        }
    }

    class Help : Command
    {
        public Help(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            String cmd = E.Data.Trim();

            if (cmd.Length > 2)
            {
                bool found = false;
                foreach (Command C in E.Owner.getCommands())
                {
                    if (C.Name.Contains(cmd) || C.Name == cmd)
                    {
                        E.Origin.Tell(" [^3" + C.Name + "^7] " + C.Description);
                        found = true;
                    }
                }

                if (!found)
                    E.Origin.Tell("Could not find that command");
            }

            else
            {
                int count = 0;
                StringBuilder helpResponse = new StringBuilder();
                List<Command> test = E.Owner.getCommands();

                foreach (Command C in test)
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
                E.Origin.Tell("Type !help <cmd> to get command usage example");
            }
        }
    }

    class FastRestart : Command
    {
        public FastRestart(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Owner.Broadcast("Performing fast restart in 5 seconds...");
            E.Owner.fastRestart(5);
        }

    }

    class MapRotate : Command
    {
        public MapRotate(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Owner.Broadcast("Performing map rotate in 5 seconds...");
            E.Owner.mapRotate(5);
        }
    }

    class SetLevel : Command
    {
        public SetLevel(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            if (E.Target == E.Origin)
            {
                E.Origin.Tell("You can't set your own level, silly.");
                return;
            }

            Player.Permission newPerm = SharedLibrary.Utilities.matchPermission(SharedLibrary.Utilities.removeWords(E.Data, 1));

            if (newPerm > Player.Permission.Banned)
            {
                E.Target.setLevel(newPerm);
                E.Target.Tell("Congratulations! You have been promoted to ^3" + newPerm);
                E.Origin.Tell(E.Target.Name + " was successfully promoted!");
                //NEEED TO MOVE
                E.Owner.clientDB.updatePlayer(E.Target);
            }

            else
                E.Origin.Tell("Invalid group specified.");
        }
    }

    class Usage : Command
    {
        public Usage(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Origin.Tell("IW4M Admin is using " + Math.Round(((System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 2048f) / 1200f), 1) + "MB");
        }
    }

    class Uptime : Command
    {
        public Uptime(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            TimeSpan uptime = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
            E.Origin.Tell(String.Format("IW4M Admin has been up for {0} days, {1} hours, and {2} minutes", uptime.Days, uptime.Hours, uptime.Minutes));
        }
    }

    class Admins : Command
    {
        public Admins(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            List<Player> activePlayers = E.Owner.getPlayers();
            lock (activePlayers)
            {
                foreach (Player P in E.Owner.getPlayers())
                {
                    if (P != null && P.Level > Player.Permission.Flagged && !P.Masked)
                    {
                        E.Origin.Tell(String.Format("[^3{0}^7] {1}", SharedLibrary.Utilities.levelToColor(P.Level), P.Name));
                    }
                }
            }
        }
    }

    class MapCMD : Command
    {
        public MapCMD(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            string newMap = E.Data.Trim().ToLower();
            foreach (Map m in E.Owner.maps)
            {
                if (m.Name.ToLower() == newMap || m.Alias.ToLower() == newMap)
                {
                    E.Owner.Broadcast("Changing to map ^2" + m.Alias);
                    SharedLibrary.Utilities.Wait(3);
                    E.Owner.Map(m.Name);
                    return;
                }
            }

            E.Owner.Broadcast("Attempting to change to unknown map ^1" + newMap);
            SharedLibrary.Utilities.Wait(3);
            E.Owner.Map(newMap);
        }
    }

    class Find : Command
    {
        public Find(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            var db_players = E.Owner.clientDB.findPlayers(E.Data.Trim());

            if (db_players == null)
            {
                E.Origin.Tell("No players found");
                return;
            }

            foreach (Player P in db_players)
            { 
                String mesg = String.Format("[^3{0}^7] [^3@{1}^7] - [{2}^7] - {3} | last seen {4} ago", P.Name, P.databaseID, SharedLibrary.Utilities.levelToColor(P.Level), P.IP, P.getLastConnection());
                E.Origin.Tell(mesg);
            }
        }
    }

    class FindAll : Command
    {
        public FindAll(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Data = E.Data.Trim();

            if (E.Data.Length < 4)
            {
                E.Origin.Tell("You must enter at least 4 letters");
                return;
            }

            //var db_players = E.Owner.clientDB.findPlayers(E.Data.Trim());
            var db_aliases = E.Owner.aliasDB.findPlayers(E.Data);

            if (db_aliases == null)
            {
                E.Origin.Tell("No players found");
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

                Player Current = E.Owner.clientDB.getPlayer(P.Number);

                if (Current != null)
                {
                    String mesg = String.Format("^1{0} ^7now goes by ^5{1}^7 [^3{2}^7]", lookingFor, Current.Name, Current.databaseID);
                    E.Origin.Tell(mesg);
                }
            }
        }
    }

    class Rules : Command
    {
        public Rules(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            if (E.Owner.rules.Count < 1)
                E.Origin.Tell("This server has not set any rules.");
            else
            {
                foreach (String r in E.Owner.rules)
                    E.Origin.Tell("- " + r);
            }
        }
    }

    class PrivateMessage : Command
    {
        public PrivateMessage(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Data = SharedLibrary.Utilities.removeWords(E.Data, 1);
            E.Target.Alert();
            E.Target.Tell("^1" + E.Origin.Name + " ^3[PM]^7 - " + E.Data);
            E.Origin.Tell(String.Format("To ^3{0} ^7-> {1}", E.Target.Name, E.Data));
        }
    }

    class Reload : Command
    {
        public Reload(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            if (E.Owner.Reload())
                E.Origin.Tell("Sucessfully reloaded configs!");
            else
                E.Origin.Tell("Unable to reload configs :(");
        }
    }

    class Balance : Command
    {
        public Balance(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Origin.currentServer.executeCommand(String.Format("admin_lastevent {0};{1}", "balance", E.Origin.npID)); //Let gsc do the magic
        }
    }

    class GoTo : Command
    {
        public GoTo(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Origin.currentServer.executeCommand(String.Format("admin_lastevent {0};{1};{2};{3}", "goto", E.Origin.npID, E.Target.Name, E.Data)); //Let gsc do the magic
        }
    }

    class Flag : Command
    {
        public Flag(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            if (E.Target.Level >= E.Origin.Level)
            {
                E.Origin.Tell("You cannot flag " + E.Target.Name);
                return;
            }

            if (E.Target.Level == Player.Permission.Flagged)
            {
                E.Target.setLevel(Player.Permission.User);
                E.Origin.Tell("You have ^5unflagged ^7" + E.Target.Name);
            }

            else
            {
                E.Target.setLevel(Player.Permission.Flagged);
                E.Origin.Tell("You have ^5flagged ^7" + E.Target.Name);
            }

            E.Owner.clientDB.updatePlayer(E.Target);
        }
    }

    class _Report : Command
    {
        public _Report(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            if (E.Owner.Reports.Find(x => x.Origin == E.Origin) != null)
            {
                E.Origin.Tell("You have already reported this player");
                return;
            }

            if (E.Target == E.Origin)
            {
                E.Origin.Tell("You cannot report yourself, silly.");
                return;
            }

            if (E.Target.Level > E.Origin.Level)
            {
                E.Origin.Tell("You cannot report " + E.Target.Name);
                return;
            }

            E.Data = SharedLibrary.Utilities.removeWords(E.Data, 1);
            E.Owner.Reports.Add(new Report(E.Target, E.Origin, E.Data));

            Connection Screenshot = new Connection(String.Format("http://server.nbsclan.org/screen.php?id={0}&name={1}?save=1", SharedLibrary.Utilities.getForumIDFromStr(E.Target.npID), E.Origin.Name));
            String Response = Screenshot.Read();

            E.Origin.Tell("Successfully reported " + E.Target.Name);

            E.Owner.ToAdmins(String.Format("^5{0}^7->^1{1}^7: {2}", E.Origin.Name, E.Target.Name, E.Data));
        }
    }

    class Reports : Command
    {
        public Reports(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            if (E.Data != null && E.Data.ToLower().Contains("clear"))
            {
                E.Owner.Reports = new List<Report>();
                E.Origin.Tell("Reports successfully cleared!");
                return;
            }

            if (E.Owner.Reports.Count < 1)
            {
                E.Origin.Tell("No players reported yet.");
                return;
            }

            int count = E.Owner.Reports.Count - 1;
            for (int i = 0; i <= count; i++)
            {
                if (count > 8)
                    i = count - 8;
                Report R = E.Owner.Reports[i];
                E.Origin.Tell(String.Format("^5{0}^7->^1{1}^7: {2}", R.Origin.Name, R.Target.Name, R.Reason));
            }
        }
    }

    class _Tell : Command
    {
        public _Tell(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Data = SharedLibrary.Utilities.removeWords(E.Data, 1);
            E.Origin.currentServer.executeCommand(String.Format("admin_lastevent tell;{0};{1};{2}", E.Origin.npID, E.Target.npID, E.Data));
        }
    }

    class Mask : Command
    {
        public Mask(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            if (E.Origin.Masked)
            {
                E.Origin.Masked = false;
                E.Origin.Tell("You are now unmasked");
            }
            else
            {
                E.Origin.Masked = true;
                E.Origin.Tell("You are now masked");
            }
        }
    }

    class BanInfo : Command
    {
        public BanInfo(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            if (E.Target == null)
            {
                E.Origin.Tell("No bans for that player.");
                return;
            }

            Penalty B = E.Owner.Bans.Find(b => b.npID.Equals(E.Target.npID));
            
            if (B == null)
            {
                E.Origin.Tell("No active ban was found for that player.");
                return;
            }

            Player Banner = E.Owner.clientDB.getPlayer(B.bannedByID, -1);

            if (Banner == null)
            {
                E.Origin.Tell("Ban was found for the player, but origin of the ban is unavailable.");
                return;
            }

            E.Origin.Tell(String.Format("^1{0} ^7was banned by ^5{1} ^7for: {2}", E.Target.Name, Banner.Name, B.Reason));
        }
    }

    class Alias : Command
    {
        public Alias(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Target.Alias = E.Owner.aliasDB.getPlayer(E.Target.databaseID);

            if (E.Target.Alias == null)
            {
                E.Target.Tell("Could not find alias info for that player.");
                return;
            }

            E.Target.Tell("[^3" + E.Target.Name + "^7]");
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
            E.Origin.Tell(message.ToString());

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

                E.Origin.Tell(message.ToString());
            }
        }
    }

    class _RCON : Command
    {
        public _RCON(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Origin.currentServer.executeCommand(E.Data.Trim());
            E.Origin.Tell("Successfuly sent RCON command!");
        }
    }

    class Plugins : Command
    {
        public Plugins(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Origin.Tell("^5Loaded Plugins:");
            foreach (Plugin P in PluginImporter.potentialPlugins)
            {
                E.Origin.Tell(String.Format("^3{0} ^7[^3{1}^7] by ^5{2}^7", P.Name, P.Version, P.Author));
            }
        }
    }
}
    