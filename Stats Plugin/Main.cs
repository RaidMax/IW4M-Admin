using System;
using SharedLibrary;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Data;

namespace SamplePlugin
{
#if SAMPLE_CODE
    public class SampleCommand : Command
    {
        public SampleCommand() : base("testplugin", "sample plugin command. syntax !testplugin", "tp", Player.Permission.User, 0, false) { }

        public override void Execute(Event E)
        {
            Player clientWhoSent = E.Origin;
            Server originatingServer = E.Owner;

            String[] messageToClient = { 
                                           String.Format("The command {0} was requested by ^3{1}", Name, clientWhoSent.Name), 
                                           String.Format("The command was request on server ^1{0}", originatingServer.getName()) 
                                       };

            foreach (String Line in messageToClient)
                clientWhoSent.Tell(Line);
        }
    }

    public class AnotherSampleCommand : Command
    {
        public AnotherSampleCommand() : base("scream", "broadcast your message. syntax !scream <message>", "s", Player.Permission.Moderator, 1, false) { }

        public override void Execute(Event E)
        {
            Server originatingServer = E.Owner;
            String Message = E.Data;
            String Sender = E.Origin.Name;

            for (int i = 0; i < 10; i++)
                originatingServer.Broadcast(String.Format("^7{0}: ^{1}{2}^7", Sender, i, Message));

            originatingServer.Log.Write("This line is coming from the plugin " + this.Name, Log.Level.Production);
        }
    }

    public class SampleEvent : EventNotify
    {
        public override void onLoad()
        {
            Console.WriteLine("The sample event plugin was loaded!");
        }
        public override void onEvent(Event E)
        {
            E.Owner.Broadcast("An event occured of type: ^1" + E.Type);

            if (E.Data != null)
                E.Origin.Tell(E.Data);
        }
    }
    
    public class InvalidCommandExample
    {
        private void doNotDoThis() { }
    }
#endif

    public class StatCommand : Command
    {
        public StatCommand() : base("stats", "view your stats. syntax !stats", "xlrstats", Player.Permission.User, 0, false) { }

        public override void Execute(Event E)
        {
            PlayerStats pStats = Stats.playerStats.getStats(E.Origin);
            String statLine = String.Format("^5{0} ^7KILLS | ^5{1} ^7DEATHS | ^5{2} ^7KDR | ^5{3} ^7SKILL", pStats.Kills, pStats.Deaths, pStats.KDR, pStats.Skill);
            E.Origin.Tell(statLine);
        }
    }

    public class Stats : Plugin
    {
        public static StatsDB playerStats { get; private set; }
        private DateTime[] lastKill = new DateTime[18];
        private DateTime[] connectionTime = new DateTime[18];
        private int[] inactiveMinutes = new int[18];
        private int[] Kills = new int[18];
        private int[] deathStreaks = new int[18];
        private int[] killStreaks = new int[18];

        public override void onEvent(Event E)
        {
            playerStats = new StatsDB("stats_" + E.Owner.getPort() + ".rm");

            if (E.Type == Event.GType.Connect)
            {
                resetCounters(E.Origin.clientID);
            }

            if (E.Type == Event.GType.MapEnd)
            {
                foreach (Player P in E.Owner.getPlayers())
                {
                    calculateAndSaveSkill(P);
                    resetCounters(P.clientID);

                    E.Owner.Log.Write("Updated skill for client #" + P.databaseID, Log.Level.Debug);
                    //E.Owner.Log.Write(String.Format("\r\nJoin: {0}\r\nInactive Minutes: {1}\r\nnewPlayTime: {2}\r\nnewSPM: {3}\r\nkdrWeight: {4}\r\nMultiplier: {5}\r\nscoreWeight: {6}\r\nnewSkillFactor: {7}\r\nprojectedNewSkill: {8}\r\nKills: {9}\r\nDeaths: {10}", connectionTime[P.clientID].ToShortTimeString(), inactiveMinutes[P.clientID], newPlayTime, newSPM, kdrWeight, Multiplier, scoreWeight, newSkillFactor, disconnectStats.Skill, disconnectStats.Kills, disconnectStats.Deaths));
                }
            }

            if (E.Type == Event.GType.Disconnect)
            {
                calculateAndSaveSkill(E.Origin);
                E.Owner.Log.Write("Updated skill for disconnecting client #" + E.Origin.databaseID, Log.Level.Debug);
            }

            if (E.Type == Event.GType.Kill)
            {
                Player Killer = E.Origin;
                PlayerStats killerStats = playerStats.getStats(Killer);

                lastKill[E.Origin.clientID] = DateTime.Now;
                Kills[E.Origin.clientID]++;

                if ((lastKill[E.Origin.clientID] - DateTime.Now).TotalSeconds > 60)
                {
                    inactiveMinutes[E.Origin.clientID]++;
                }

                if (Killer != E.Target)
                {
                    killerStats.Kills++;

                    if (killerStats.Deaths == 0)
                        killerStats.KDR = killerStats.Kills;
                    else
                        killerStats.KDR = (double)killerStats.Kills / (double)killerStats.Deaths;

                    playerStats.updateStats(Killer, killerStats);

                    killStreaks[E.Origin.clientID]++;
                    deathStreaks[E.Origin.clientID] = 0;
                }

                Killer.Tell(messageOnStreak(killStreaks[E.Origin.clientID], deathStreaks[E.Origin.clientID]));
            }

            if (E.Type == Event.GType.Death)
            {
                Player Victim = E.Origin;
                PlayerStats victimStats = playerStats.getStats(Victim);

                victimStats.Deaths++;
                victimStats.KDR = (double)victimStats.Kills / (double)victimStats.Deaths;

                playerStats.updateStats(Victim, victimStats);

                deathStreaks[E.Origin.clientID]++;
                killStreaks[E.Origin.clientID] = 0;

                Victim.Tell(messageOnStreak(killStreaks[E.Origin.clientID], deathStreaks[E.Origin.clientID]));
            }
        }

        private void calculateAndSaveSkill(Player P)
        {
            PlayerStats disconnectStats = playerStats.getStats(P);
            if (Kills[P.clientID] == 0)
                return;

            else if (lastKill[P.clientID] > connectionTime[P.clientID])
                inactiveMinutes[P.clientID] += (int)(DateTime.Now - lastKill[P.clientID]).TotalMinutes;

            int newPlayTime = (int)(DateTime.Now - connectionTime[P.clientID]).TotalMinutes - inactiveMinutes[P.clientID];
            double newSPM = Kills[P.clientID] * 50 / Math.Max(newPlayTime, 1);

            double kdrWeight = Math.Round(Math.Pow(disconnectStats.KDR, 2 / Math.E), 3);
            double Multiplier;

            if (disconnectStats.scorePerMinute == 1)
                Multiplier = 1;
            else
                Multiplier = newSPM / disconnectStats.scorePerMinute;

            double scoreWeight = (newSPM * (newPlayTime / disconnectStats.playTime));
            double newSkillFactor = Multiplier * scoreWeight;

            if (Multiplier >= 1)
                disconnectStats.scorePerMinute += newSkillFactor;
            else
                disconnectStats.scorePerMinute -= (scoreWeight - newSkillFactor);

            disconnectStats.Skill = disconnectStats.scorePerMinute * kdrWeight / 10;
            disconnectStats.playTime += newPlayTime;

            playerStats.updateStats(P, disconnectStats);
        }

        private void resetCounters(int cID)
        {
            Kills[cID] = 0;
            connectionTime[cID] = DateTime.Now;
            inactiveMinutes[cID] = 0;
            deathStreaks[cID] = 0;
            killStreaks[cID] = 0;
        }


        public override void onLoad()
        {
            for (int i = 0; i < 18; i++)
            {
                Kills[i] = 0;
                connectionTime[i] = DateTime.Now;
                inactiveMinutes[i] = 0;
                deathStreaks[i] = 0;
                killStreaks[i] = 0;
            }
        }

        public override void onUnload()
        {

        }

        private String messageOnStreak(int killStreak, int deathStreak)
        {
            String Message = "";
            switch (killStreak)
            {
                case 5:
                    Message = "Great job! You're on a ^55 killstreak!";
                    break;
                case 10:
                    Message = "Amazing! ^510 kills ^7without dying!";
                    break;
            }

            switch (deathStreak)
            {
                case 5:
                    Message = "Pick it up soldier, you've died ^55 times ^7in a row...";
                    break;
                case 10:
                    Message = "Seriously? ^510 deaths ^7without getting a kill?";
                    break;
            }

            return Message;
        }

        public override string Name
        {
            get
            {
                return "Basic Stats";
            }
        }

        public override float Version
        {
            get
            {
                return 0.2f;
            }
        }

        public override string Author
        {
            get
            {
                return "RaidMax";
            }
        }
    }

    public class StatsDB : Database
    {
        public StatsDB(String FN) : base(FN) { }

        public override void Init()
        {
            if (!File.Exists(FileName))
            {
                String Create = "CREATE TABLE [STATS] ( [npID] TEXT, [KILLS] INTEGER DEFAULT 0, [DEATHS] INTEGER DEFAULT 0, [KDR] TEXT DEFAULT 0, [SKILL] TEXT DEFAULT 0, [MEAN] REAL DEFAULT 0, [DEV] REAL DEFAULT 0, [SPM] TEXT DEFAULT 0, [PLAYTIME] INTEGER DEFAULT 0);";
                ExecuteNonQuery(Create);
            }
        }

        public void addPlayer(Player P)
        {
            Dictionary<String, object> newPlayer = new Dictionary<String, object>();

            newPlayer.Add("npID", P.npID);
            newPlayer.Add("KILLS", 0);
            newPlayer.Add("DEATHS", 0);
            newPlayer.Add("KDR", 0.0);
            newPlayer.Add("SKILL", 1.0);
            newPlayer.Add("SPM", 1.0);
            newPlayer.Add("PLAYTIME", 1.0);

            Insert("STATS", newPlayer);
        }

        public PlayerStats getStats(Player P)
        {
            String Query = String.Format("SELECT * FROM STATS WHERE npID = '{0}'", P.npID);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow ResponseRow = Result.Rows[0];
                return new PlayerStats(
                                        Convert.ToInt32(ResponseRow["KILLS"]),
                                        Convert.ToInt32(ResponseRow["DEATHS"]),
                                        Convert.ToDouble(ResponseRow["KDR"]),
                                        Convert.ToDouble(ResponseRow["SKILL"]),
                                        Convert.ToDouble(ResponseRow["SPM"]),
                                        Convert.ToInt32(ResponseRow["PLAYTIME"])
                                      );
            }

            else
            {
                addPlayer(P);
                return getStats(P);
            }
        }

        public void updateStats(Player P, PlayerStats S)
        {
            Dictionary<String, object> updatedPlayer = new Dictionary<String, object>();

            updatedPlayer.Add("KILLS", S.Kills);
            updatedPlayer.Add("DEATHS", S.Deaths);
            updatedPlayer.Add("KDR", Math.Round(S.KDR, 2));
            updatedPlayer.Add("SKILL", Math.Round(S.Skill, 1));
            updatedPlayer.Add("SPM", Math.Round(S.scorePerMinute, 0));
            updatedPlayer.Add("PLAYTIME", S.playTime);

            Update("STATS", updatedPlayer, String.Format("npID = '{0}'", P.npID));
        }
    }

    public struct PlayerStats
    {
        public PlayerStats(int K, int D, double DR, double S, double sc, int P)
        {
            Kills = K;
            Deaths = D;
            KDR = DR;
            Skill = S;
            scorePerMinute = sc;
            playTime = P;
        }

        public int Kills;
        public int Deaths;
        public double KDR;
        public double Skill;
        public double scorePerMinute;
        public int playTime;
    }
}