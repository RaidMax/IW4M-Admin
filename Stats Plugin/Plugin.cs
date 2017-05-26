using System;
using SharedLibrary;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Data;

using SharedLibrary.Extensions;
using System.Threading.Tasks;

namespace StatsPlugin
{
    public class StatCommand : Command
    {
        public StatCommand() : base("stats", "view your stats. syntax !stats", "xlrstats", Player.Permission.User, 0, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            String statLine;
            PlayerStats pStats;

            if (E.Target != null)
            {
                pStats = Stats.statLists.Find(x => x.Port == E.Owner.getPort()).playerStats.getStats(E.Target);
                statLine = String.Format("^5{0} ^7KILLS | ^5{1} ^7DEATHS | ^5{2} ^7KDR | ^5{3} ^7SKILL", pStats.Kills, pStats.Deaths, pStats.KDR, pStats.Skill);
            }

            else
            {
                pStats = Stats.statLists.Find(x => x.Port == E.Owner.getPort()).playerStats.getStats(E.Origin);
                statLine = String.Format("^5{0} ^7KILLS | ^5{1} ^7DEATHS | ^5{2} ^7KDR | ^5{3} ^7SKILL", pStats.Kills, pStats.Deaths, pStats.KDR, pStats.Skill);
            }

            await E.Origin.Tell(statLine);
        }
    }

    public class TopStats : Command
    {
        public TopStats() : base("topstats", "view the top 5 players on this server. syntax !topstats", "!ts", Player.Permission.User, 0, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            List<KeyValuePair<String, PlayerStats>> pStats = Stats.statLists.Find(x => x.Port == E.Owner.getPort()).playerStats.topStats();
            StringBuilder msgBlder = new StringBuilder();

            await E.Origin.Tell("^5--Top Players--");
            foreach (KeyValuePair<String, PlayerStats> pStat in pStats)
            {
                Player P = E.Owner.clientDB.getPlayer(pStat.Key, -1);
                if (P == null)
                    continue;
                await E.Origin.Tell(String.Format("^3{0}^7 - ^5{1} ^7KDR | ^5{2} ^7SKILL", P.Name, pStat.Value.KDR, pStat.Value.Skill));
            }
        }
    }

    /// <summary>
    /// Each server runs from the same plugin ( for easier reloading and reduced memory usage ).
    /// So, to have multiple stat tracking, we must store a stat struct for each server
    /// </summary>
    public class Stats : IPlugin
    {
        public static List<StatTracking> statLists;

        public struct StatTracking
        {
            public StatsDB playerStats;
            public DateTime[] lastKill, connectionTime;
            public int[] inactiveMinutes, Kills, deathStreaks, killStreaks;
            public int Port;

            public StatTracking(int port)
            {
                playerStats = new StatsDB("Database/stats_" + port + ".rm");
                inactiveMinutes = new int[18];
                Kills = new int[18];
                deathStreaks = new int[18];
                killStreaks = new int[18];
                lastKill = new DateTime[18];
                connectionTime = new DateTime[18];
                Port = port;
            }
        }

        public string Name
        {
            get { return "Basic Stats"; }
        }

        public float Version
        {
            get { return 1f; }
        }

        public string Author
        {
            get { return "RaidMax"; }
        }

        public async Task OnLoad()
        {
            statLists = new List<StatTracking>();
            await Task.Delay(0);
        }

        public async Task OnUnload()
        {
            statLists.Clear();
            await Task.Delay(0);
        }

        public async Task OnTick(Server S)
        {
            await Task.Delay(0);
        }

        public async Task OnEvent(Event E, Server S)
        {
            if (E.Type == Event.GType.Start)
            {
                statLists.Add(new StatTracking(S.getPort()));
            }

            if (E.Type == Event.GType.Stop)
            {
                statLists.RemoveAll(x => x.Port == S.getPort());
            }

            if (E.Type == Event.GType.Connect)
            {
                resetCounters(E.Origin.clientID, S.getPort());

                PlayerStats checkForTrusted = statLists.Find(x => x.Port == S.getPort()).playerStats.getStats(E.Origin);
                if (checkForTrusted.playTime >= 4320 && E.Origin.Level < Player.Permission.Trusted)
                {
                    E.Origin.setLevel(Player.Permission.Trusted);
                    E.Owner.clientDB.updatePlayer(E.Origin);
                    await E.Origin.Tell("Congratulations, you are now a ^5trusted ^7player! Type ^5!help ^7to view new commands.");
                    await E.Origin.Tell("You earned this by playing for ^53 ^7full days!");
                }
            }

            if (E.Type == Event.GType.MapEnd || E.Type == Event.GType.Stop)
            {
                foreach (Player P in S.getPlayers())
                {

                    if (P == null)
                        continue;

                    calculateAndSaveSkill(P, statLists.Find(x =>x.Port == S.getPort()));
                    resetCounters(P.clientID, S.getPort());

                    E.Owner.Log.Write("Updated skill for client #" + P.databaseID, Log.Level.Debug);
                    //E.Owner.Log.Write(String.Format("\r\nJoin: {0}\r\nInactive Minutes: {1}\r\nnewPlayTime: {2}\r\nnewSPM: {3}\r\nkdrWeight: {4}\r\nMultiplier: {5}\r\nscoreWeight: {6}\r\nnewSkillFactor: {7}\r\nprojectedNewSkill: {8}\r\nKills: {9}\r\nDeaths: {10}", connectionTime[P.clientID].ToShortTimeString(), inactiveMinutes[P.clientID], newPlayTime, newSPM, kdrWeight, Multiplier, scoreWeight, newSkillFactor, disconnectStats.Skill, disconnectStats.Kills, disconnectStats.Deaths));
                }
            }

            if (E.Type == Event.GType.Disconnect)
            {
                calculateAndSaveSkill(E.Origin, statLists.Find(x=>x.Port == S.getPort()));
                resetCounters(E.Origin.clientID, S.getPort());
                E.Owner.Log.Write("Updated skill for disconnecting client #" + E.Origin.databaseID, Log.Level.Debug);
            }

            if (E.Type == Event.GType.Kill)
            {
                if (E.Origin == E.Target || E.Origin == null)
                    return;

                Player Killer = E.Origin;
                StatTracking curServer = statLists.Find(x => x.Port == S.getPort());
                PlayerStats killerStats = curServer.playerStats.getStats(Killer);


                curServer.lastKill[E.Origin.clientID] = DateTime.Now;
                curServer.Kills[E.Origin.clientID]++;

                if ((curServer.lastKill[E.Origin.clientID] - DateTime.Now).TotalSeconds > 60)
                    curServer.inactiveMinutes[E.Origin.clientID]++;
 
                killerStats.Kills++;

                if (killerStats.Deaths == 0)
                    killerStats.KDR = killerStats.Kills;
                else
                    killerStats.KDR = Math.Round((double)killerStats.Kills / (double)killerStats.Deaths, 2);

                curServer.playerStats.updateStats(Killer, killerStats);

                curServer.killStreaks[Killer.clientID] += 1;
                curServer.deathStreaks[Killer.clientID] = 0;

                await Killer.Tell(messageOnStreak(curServer.killStreaks[Killer.clientID], curServer.deathStreaks[Killer.clientID]));
            }

            if (E.Type == Event.GType.Death)
            {
                if (E.Origin == E.Target || E.Origin == null)
                    return;

                Player Victim = E.Origin;
                StatTracking curServer = statLists.Find(x => x.Port == S.getPort());
                PlayerStats victimStats = curServer.playerStats.getStats(Victim);
               
                victimStats.Deaths++;
                victimStats.KDR = Math.Round((double)victimStats.Kills / (double)victimStats.Deaths, 2);

                curServer.playerStats.updateStats(Victim, victimStats);

                curServer.deathStreaks[Victim.clientID] += 1;
                curServer.killStreaks[Victim.clientID] = 0;

                await Victim.Tell(messageOnStreak(curServer.killStreaks[Victim.clientID], curServer.deathStreaks[Victim.clientID]));
            }
        }

        private void calculateAndSaveSkill(Player P, StatTracking curServer)
        {
            if (P == null)
                return;

            PlayerStats disconnectStats = curServer.playerStats.getStats(P);
            if (curServer.Kills[P.clientID] == 0)
                return;

            else if (curServer.lastKill[P.clientID] > curServer.connectionTime[P.clientID])
                curServer.inactiveMinutes[P.clientID] += (int)(DateTime.Now - curServer.lastKill[P.clientID]).TotalMinutes;

            int newPlayTime = (int)(DateTime.Now - curServer.connectionTime[P.clientID]).TotalMinutes - curServer.inactiveMinutes[P.clientID];

            if (newPlayTime < 2)
                return;
 
            double newSPM = curServer.Kills[P.clientID] * 50 / Math.Max(1, newPlayTime);
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

            curServer.playerStats.updateStats(P, disconnectStats);
        }

        private void resetCounters(int cID, int serverPort)
        {
            StatTracking selectedPlayers = statLists.Find(x => x.Port == serverPort);

            selectedPlayers.Kills[cID] = 0;
            selectedPlayers.connectionTime[cID] = DateTime.Now;
            selectedPlayers.inactiveMinutes[cID] = 0;
            selectedPlayers.deathStreaks[cID] = 0;
            selectedPlayers.killStreaks[cID] = 0;
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
            updatedPlayer.Add("SPM", Math.Round(S.scorePerMinute, 1));
            updatedPlayer.Add("PLAYTIME", S.playTime);

            Update("STATS", updatedPlayer, new KeyValuePair<string, object>("npID", P.npID));
        }

        public List<KeyValuePair<String, PlayerStats>> topStats()
        {
            String Query = String.Format("SELECT * FROM STATS WHERE SKILL > 0 AND KDR < '{0}' AND KILLS > '{1}' AND PLAYTIME > '{2}' ORDER BY SKILL DESC LIMIT '{3}'", 10, 150, 60, 5);
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