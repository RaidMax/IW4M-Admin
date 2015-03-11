using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin
{
    abstract class Command
    {
        public Command(String N, String D, String U, Player.Permission P, int args, bool nT)
        {
            Name = N;
            Description = D;
            Usage = U;
            Permission = P;
            Arguments = args;
            hasTarget = nT;
        }

        //Get command name
        public String getName()
        {
            return Name;
        }
        //Get description on command
        public String getDescription()
        {
            return Description;
        }
        //Get the example usage of the command
        public String getAlias()
        {
            return Usage;
        }
        //Get the required permission to execute the command
        public Player.Permission getNeededPerm()
        {
            return Permission;
        }

        public int getNumArgs()
        {
            return Arguments;
        }     

        public bool needsTarget()
        {
            return hasTarget;
        }

        //Execute the command
        abstract public void Execute(Event E);

        private String Name;
        private String Description;
        private String Usage;
        private int Arguments;
        private bool hasTarget;

        public Player.Permission Permission;
    }

    class Owner : Command
    {
        public Owner(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            if (E.Owner.owner == null)
            {
                E.Origin.setLevel(Player.Permission.Owner);
                E.Origin.Tell("Congratulations, you have claimed ownership of this server!");
                E.Owner.owner = E.Origin;
                E.Owner.DB.updatePlayer(E.Origin);
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
            if (E.Origin.getLevel() <= E.Target.getLevel())
                E.Origin.Tell("You cannot warn " + E.Target.getName());
            else
            {
                E.Target.LastOffense = Utilities.removeWords(E.Data, 1);
                E.Target.Warnings++;
                String Message = String.Format("^1WARNING ^7[^3{0}^7]: ^3{1}^7, {2}", E.Target.Warnings, E.Target.getName(), E.Target.LastOffense);
                E.Owner.Broadcast(Message);
                if (E.Target.Warnings >= 4)
                    E.Target.Kick("You were kicked for too many warnings!");
            }
                
        }

    }

    class WarnClear : Command
    {
        public WarnClear(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Target.LastOffense = String.Empty;
            E.Target.Warnings = 0;
            String Message = String.Format("All warning cleared for {0}", E.Target.getName());
            E.Owner.Broadcast(Message);
        }

    }

    class Kick : Command
    {
        public Kick(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Target.LastOffense = Utilities.removeWords(E.Data, 1);
            String Message = "^1Player Kicked: ^5" + E.Target.LastOffense + "              ^1Admin: ^5" + E.Origin.getName();
            if (E.Origin.getLevel() > E.Target.getLevel())
                E.Target.Kick(Message);
            else
                E.Origin.Tell("You cannot kick " + E.Target.getName());            
        }

    }

    class Say : Command
    {
        public Say(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Owner.Broadcast("^1" + E.Origin.getName() + " - ^6" + E.Data + "^7");
        }

    }

    class TempBan : Command
    {
        public TempBan(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Target.LastOffense = Utilities.removeWords(E.Data, 1);
            String Message = "^1Player Temporarily Banned: ^5" + E.Target.LastOffense + "^7 (1 hour)";
            if (E.Origin.getLevel() > E.Target.getLevel())
                E.Target.tempBan(Message);
            else
                E.Origin.Tell("You cannot temp ban " + E.Target.getName());

        }

    }

    class SBan : Command
    {
        public SBan(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            E.Target.LastOffense = Utilities.removeWords(E.Data, 1);
            E.Target.lastEvent = E; // needs to be fixed
            String Message;
#if DEBUG
            Message = "^1Player Banned: ^5" + E.Target.LastOffense + "^7 (appeal at nbsclan.org)";
#else
            if (E.Owner.Website == null)
                Message = "^1Player Banned: ^5" + E.Target.LastOffense;
            else
                Message = "^1Player Banned: ^5" + E.Target.LastOffense + "^7 (appeal at " + E.Owner.Website + ")";

#endif
            if (E.Origin.getLevel() > E.Target.getLevel())
            {
                E.Target.Ban(Message, E.Origin);
                E.Origin.Tell(String.Format("Sucessfully banned ^5{0} ^7({1})", E.Target.getName(), E.Target.getID()));
            }
            else
                E.Origin.Tell("You cannot ban " + E.Target.getName());
        }

    }

    class Unban : Command
    {
        public Unban(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            if (E.Owner.Unban(E.Data.Trim(), E.Target))
                E.Origin.Tell("Successfully unbanned " + E.Data.Trim());
            else
                E.Origin.Tell("Unable to find a ban for that GUID");
        }

    }

    class WhoAmI : Command
    {
        public WhoAmI(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            String You = String.Format("You are {0} at client spot {1} with xuid {2} and ID {5}. You have connected {3} times and are currently ranked {4}", E.Origin.getName(), E.Origin.getClientNum(), E.Origin.getID(), E.Origin.getConnections(), E.Origin.getLevel(), E.Origin.getDBID());
            E.Origin.Tell(You);
        }

    }

    class List : Command
    {
        public List(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            foreach (Player P in E.Owner.getPlayers())
            {
                if (P == null)
                    continue;

                E.Origin.Tell(String.Format("[^3{0}^7]{3}[^3{1}^7] {2}", Utilities.levelToColor(P.getLevel()), P.getClientNum(), P.getName(), Utilities.getSpaces(Player.Permission.SeniorAdmin.ToString().Length - P.getLevel().ToString().Length)));
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
                    if (C.getName().Contains(cmd) || C.getName() == cmd)
                    {
                        E.Origin.Tell(" [^3" + C.getName() + "^7] " + C.getDescription());
                        found = true;
                    }
                }

                if (!found)
                    E.Origin.Tell("Could not find that command");
            }

            else
            {
                int count = 0;
                String _commands = String.Empty;

                foreach (Command C in E.Owner.getCommands())
                {        
                    if (E.Origin.getLevel() >= C.getNeededPerm())
                    {
                        _commands = _commands + " [^3" + C.getName() + "^7] ";
                        if (count >= 3)
                        {
                            E.Origin.Tell(_commands);
                            _commands = String.Empty;
                            count = 0;
                        }
                        count++;
                    }
                }
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

            Player.Permission newPerm = Utilities.matchPermission(Utilities.removeWords(E.Data, 1));

            if (newPerm > Player.Permission.Banned)
            {
                E.Target.setLevel(newPerm);
                E.Target.Tell("Congratulations! You have been promoted to ^3" + newPerm);
                E.Origin.Tell(E.Target.getName() + " was successfully promoted!");
                //NEEED TO mOVE 
                E.Owner.DB.updatePlayer(E.Target);
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
            foreach (Player P in E.Owner.getPlayers())
            {
                if (P != null && P.getLevel() > Player.Permission.User)
                {
                    E.Origin.Tell(String.Format("[^3{0}^7] {1}", Utilities.levelToColor(P.getLevel()), P.getName()));
                }
            }
        }

    }

    class Wisdom : Command
    {
        public Wisdom(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            String Quote = new Connection("http://www.iheartquotes.com/api/v1/random?max_lines=1&max_characters=200").Read();
            E.Owner.Broadcast(Utilities.removeNastyChars(Quote));
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
                    Utilities.Wait(3);
                    E.Owner.Map(m.Name);
                    return;
                }
            }

            E.Owner.Broadcast("Attempting to change to unknown map ^1" + newMap);
            Utilities.Wait(3);
            E.Owner.Map(newMap);
        }

    }

    class Find : Command
    {
        public Find(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            var db_players = E.Owner.DB.findPlayers(E.Data.Trim());
            if (db_players == null)
            {
                E.Origin.Tell("No players found");
                return;
            }

            foreach (Player P in db_players)
            {
                String mesg = String.Format("[^3{0}^7] [^3@{1}^7] - {2} [{3}^7]", P.getName(), P.getDBID(), P.getID(), Utilities.levelToColor(P.getLevel()));
                E.Origin.Tell(mesg);
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
            E.Target.Tell("^1" + E.Origin.getName() + " ^3[PM]^7 - " + E.Data);
            E.Origin.Tell("Sucessfully sent message");
        }
    }

    class _Stats : Command
    {
        public _Stats(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            if (E.Target == null)
                E.Origin.Tell(String.Format("^5{0} ^7KILLS | ^5{1} ^7DEATHS | ^5{2} ^7KDR | ^5{3} ^7SKILL", E.Origin.stats.Kills, E.Origin.stats.Deaths, E.Origin.stats.KDR, E.Origin.stats.Skill));
            else
                E.Origin.Tell(String.Format("[^3{4}^7] ^5{0} ^7KILLS | ^5{1} ^7DEATHS | ^5{2} ^7KDR | ^5{3} ^7SKILL", E.Target.stats.Kills, E.Target.stats.Deaths, E.Target.stats.KDR, E.Target.stats.Skill, E.Target.getName()));
        }
    }

    class TopStats : Command
    {
        public TopStats(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override void Execute(Event E)
        {
            List<Stats> Top = E.Owner.stats.topStats();
            List<Player> TopP = new List<Player>();

            foreach (Stats S in Top)
            {
                Player P = E.Owner.DB.findPlayers(S.Kills); // BAD
                if (P != null)
                {
                    P.stats = S;
                    TopP.Add(P);
                }
            }
            if (TopP.Count > 0)
            {
                E.Origin.Tell("^1TOP PLAYERS");
                foreach (Player P in TopP)
                {
                    if (P != null)
                        E.Origin.Tell(String.Format("^3{0}^7 - ^5{1} ^7KDR | ^5{2} ^7SKILL", P.getName(), P.stats.KDR, P.stats.Skill));

                }
            }
            else
                E.Origin.Tell("There are no top players yet!");
        }
    }


}
