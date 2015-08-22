using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace SharedLibrary
{
    public class Aliases
    {
        public Aliases(int Num, String N, String I)
        {
            Number = Num;
            Names = N.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
            IPS = new List<String>(I.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
        }

        public List<String> Names { get; private set; }
        public List<String> IPS { get; private set; }
        public int Number { get; private set; }
    }

    public class Player
    {
        public enum Permission
        {
            Banned = -1,
            User = 0,
            Flagged = 1,
            Moderator = 2,
            Administrator = 3,
            SeniorAdmin = 4,
            Owner = 5,
            Creator = 6,
            Console = 7,
        }

        public Player(string n, string id, int num, int l)
        {
            Name = n;
            npID = id;
            clientID = num;
            Level = (Player.Permission)l;
            lastOffense = String.Empty;
            Connections = 0;
            IP = "";
            Warnings = 0;
            Alias = new Aliases(0, "", "");
            LastConnection = DateTime.Now;

        }

        public Player(string n, string id, int num, String I)
        {
            Name = n;
            npID = id;
            clientID = num;
            IP = I;
            LastConnection = DateTime.Now;
        }

        public Player(string n, string id, Player.Permission P, String I)
        {
            Name = n;
            npID = id;
            Level = P;
            IP = I;
        }

        public Player(string n, string id, int num, Player.Permission l, int cind, String lo, int con, String IP2)
        {
            Name = n;
            npID = id;
            clientID = num;
            Level = l;
            databaseID = cind;
            if (lo == null)
                lastOffense = String.Empty;
            else
                lastOffense = lo;
            Connections = con;
            IP = IP2;
            Warnings = 0;
            Masked = false;
            LastConnection = DateTime.Now;
        }

        public Player(string n, string id, int num, Player.Permission l, int cind, String lo, int con, String IP2, DateTime LC)
        {
            Name = n;
            npID = id;
            clientID = num;
            Level = l;
            databaseID = cind;
            if (lo == null)
                lastOffense = String.Empty;
            else
                lastOffense = lo;
            Connections = con;
            IP = IP2;
            Warnings = 0;
            Masked = false;
            LastConnection = LC;
        }

        public String getLastConnection()
        {
            return Utilities.timePassed(LastConnection);
        }

        public void updateName(String n)
        {
            if (n.Trim() != String.Empty)
                Name = n;
        }

        public void updateIP(String I)
        {
            IP = I;
        }

        public void setLevel(Player.Permission Perm)
        {
            Level = Perm;
        }

        public void Tell(String Message)
        {
            lastEvent.Owner.Tell(Message, this);
        }

        public void Kick(String Message)
        {
            currentServer.Kick(Message, this);
        }

        public void tempBan(String Message)
        {
            currentServer.tempBan(Message, this);
        }

        public void Ban(String Message, Player Sender)
        {
            currentServer.Ban(Message, this, Sender);
        }

        public void Alert()
        {
            currentServer.Alert(this);
        }

        public String Name { get; private set; }
        public string npID { get; private set; }
        public int clientID { get; private set; }
        public Player.Permission Level { get; private set; }
        public int databaseID { get; private set; }
        public int Connections { get; set; }
        public String IP { get; private set; }
        public DateTime LastConnection { get; private set; }
        public Server currentServer { get; private set; }

        public Event lastEvent;
        public String lastOffense;
        public int Warnings;
        public Aliases Alias;
        public bool Masked;
    }
}
