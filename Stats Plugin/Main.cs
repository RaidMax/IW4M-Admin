using System;
using SharedLibrary;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Data;

namespace StatsPlugin
{
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

    public class topStats : Command
    {
        public topStats() : base("topstats", "view the top 5 players on this server. syntax !topstats", "!ts", Player.Permission.User, 0, false) { }

        public override void Execute(Event E)
        {
            List<KeyValuePair<String, PlayerStats>> pStats = Stats.playerStats.topStats();
            StringBuilder msgBlder = new StringBuilder();

            E.Origin.Tell("^5--Top Players--");
            foreach (KeyValuePair<String, PlayerStats> pStat in pStats)
            {
                Player P = E.Owner.clientDB.getPlayer(pStat.Key, -1);
                if (P == null)
                    continue;
                E.Origin.Tell(String.Format("^3{0}^7 - ^5{1} ^7KDR | ^5{2} ^7SKILL", P.Name, pStat.Value.KDR, pStat.Value.Skill));
            }

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

                    if (P == null)
                        continue;

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
                if (E.Origin == E.Target)
                    return;

                Player Killer = E.Origin;
                PlayerStats killerStats = playerStats.getStats(Killer);

                lastKill[E.Origin.clientID] = DateTime.Now;
                Kills[E.Origin.clientID]++;

                if ((lastKill[E.Origin.clientID] - DateTime.Now).TotalSeconds > 60)
                {
                    inactiveMinutes[E.Origin.clientID]++;
                }

                killerStats.Kills++;

                if (killerStats.Deaths == 0)
                    killerStats.KDR = killerStats.Kills;
                else
                    killerStats.KDR = killerStats.Kills / killerStats.Deaths;

                playerStats.updateStats(Killer, killerStats);

                killStreaks[E.Origin.clientID]++;
                deathStreaks[E.Origin.clientID] = 0;

                Killer.Tell(messageOnStreak(killStreaks[E.Origin.clientID], deathStreaks[E.Origin.clientID]));
            }

            if (E.Type == Event.GType.Death)
            {
                Player Victim = E.Origin;
                PlayerStats victimStats = playerStats.getStats(Victim);

                victimStats.Deaths++;
                victimStats.KDR = victimStats.Kills / victimStats.Deaths;

                playerStats.updateStats(Victim, victimStats);

                deathStreaks[E.Origin.clientID]++;
                killStreaks[E.Origin.clientID] = 0;

                Victim.Tell(messageOnStreak(killStreaks[E.Origin.clientID], deathStreaks[E.Origin.clientID]));
            }
        }

        private void calculateAndSaveSkill(Player P)
        {
            if (P == null)
                return;

            PlayerStats disconnectStats = playerStats.getStats(P);
            if (Kills[P.clientID] == 0)
                return;

            else if (lastKill[P.clientID] > connectionTime[P.clientID])
                inactiveMinutes[P.clientID] += (int)(DateTime.Now - lastKill[P.clientID]).TotalMinutes;

            int newPlayTime = (int)(DateTime.Now - connectionTime[P.clientID]).TotalMinutes - inactiveMinutes[P.clientID];

            if (newPlayTime < 2)
                return;
 
            double newSPM = Kills[P.clientID] * 50 / newPlayTime;
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
                String Create = "CREATE TABLE [STATS] ( [npID] TEXT, [KILLS] INTEGER DEFAULT 0, [DEATHS] INTEGER DEFAULT 0, [KDR] REAL DEFAULT 0, [SKILL] REAL DEFAULT 0, [MEAN] REAL DEFAULT 0, [DEV] REAL DEFAULT 0, [SPM] REAL DEFAULT 0, [PLAYTIME] INTEGER DEFAULT 0);";
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

        public List<KeyValuePair<String, PlayerStats>> topStats()
        {
            String Query = String.Format("SELECT * FROM STATS WHERE KDR < '{0}' AND KILLS > '{1}' AND PLAYTIME > '{2}' ORDER BY SKILL DESC LIMIT '{3}'", 10, 150, 60, 5);
            DataTable Result = GetDataTable(Query);
            List<KeyValuePair<String, PlayerStats>> pStats = new List<KeyValuePair<String, PlayerStats>>();

            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow ResponseRow in Result.Rows)
                {
                    pStats.Add( new KeyValuePair<String, PlayerStats>(ResponseRow["npID"].ToString(), 
                        new PlayerStats(
                                        Convert.ToInt32(ResponseRow["KILLS"]),
                                        Convert.ToInt32(ResponseRow["DEATHS"]),
                                        Convert.ToDouble(ResponseRow["KDR"]),
                                        Convert.ToDouble(ResponseRow["SKILL"]),
                                        Convert.ToDouble(ResponseRow["SPM"]),
                                        Convert.ToInt32(ResponseRow["PLAYTIME"])
                                      )
                          ));
                }
            }
            return pStats;
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