using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin
{
    class Stats
    {
        public Stats(int K, int D, double kdr, double skill)
        {
            Kills = K;
            Deaths = D;
            KDR = Math.Round(kdr,2);
            Skill = Math.Round(skill,2);
        }

        public void Update()
        {
            KDR = Math.Round((double)((double)Kills / (double)Deaths), 2);
            Skill = Math.Round((double)Kills * (((double)Kills / (double)Deaths) / 10), 2);
        }

        public int Kills;
        public int Deaths;
        public double KDR;
        public double Skill;
    }

    class Player
    {
        public enum Permission
        {
            Banned = -1,
            User = 0,
            Moderator = 1,
            Administrator = 2,
            SeniorAdmin = 3,
            Owner = 4,
            Creator = 5,
        }

        public Player(string n, string id, int num, int l)
        {
            Name = n;
            npID = id;
            Number = num;
            Level = (Player.Permission)l;
            LastOffense = null;
            Connections = 0;
            Warnings = 0;
        }

        public Player(string n, string id, int num, Player.Permission l, int cind, String lo, int con)
        {
            Name = n;
            npID = id;
            Number = num;
            Level = l;
            dbID = cind;
            LastOffense = lo;
            Connections = con + 1;
            Warnings = 0;
        }

        public String getName()
        {
            return Name;
        }

        public String getID()
        {
            return npID;
        }
        
        public int getDBID()
        {
            return dbID;
        }

        public int getClientNum()
        {
            return Number;
        }

        public Player.Permission getLevel()
        {
            return Level;
        }

        public int getConnections()
        {
            return Connections;
        }

        public String getLastO()
        {
            return LastOffense;
        }

        // BECAUSE IT NEEDS TO BE CHANGED!
        public void setLevel(Player.Permission Perm)
        {
            Level = Perm;
        }

        public void Tell(String Message)
        {
            lastEvent.Owner.Tell(Message, this);
        }

        public void Warn(String Message)
        {
            lastEvent.Owner.Broadcast(Message);
        }

        public void Kick(String Message)
        {
            lastEvent.Owner.Kick(Message, this);
        }

        public void tempBan(String Message)
        {
            lastEvent.Owner.tempBan(Message, this);
        }

        public void Ban(String Message, Player Sender)
        {
            lastEvent.Owner.Ban(Message, this, Sender);
        }

        //should be moved to utils
        public Player findPlayer(String Nme)
        {
            foreach (Player P in lastEvent.Owner.getPlayers())
            {
                if (P == null)
                    continue;
                if (P.getName().ToLower().Contains(Name.ToLower()))
                    return P;
            }

            return null;
        }

        private string Name;
        private string npID;
        private int Number;
        private Player.Permission Level;
        private int dbID;
        private int Connections;

        public Event lastEvent;
        public String LastOffense;
        public int Warnings;
        public Stats stats;
    }
}
