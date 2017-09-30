using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

using SharedLibrary;

namespace StatsPlugin
{
    public class CViewStats : Command
    {
        public CViewStats() : base("stats", "view your stats. syntax !stats", "xlrstats", Player.Permission.User, 0, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            String statLine;
            PlayerStats pStats;

            if (E.Data.Length > 0 && E.Target == null)
            {
                await E.Origin.Tell("Cannot find the player you specified");
                return;
            }

            if (E.Target != null)
            {
                pStats = Stats.statLists.Find(x => x.Port == E.Owner.GetPort()).playerStats.GetStats(E.Target);
                statLine = String.Format("^5{0} ^7KILLS | ^5{1} ^7DEATHS | ^5{2} ^7KDR | ^5{3} ^7SKILL", pStats.Kills, pStats.Deaths, pStats.KDR, pStats.Skill);
            }

            else
            {
                pStats = Stats.statLists.Find(x => x.Port == E.Owner.GetPort()).playerStats.GetStats(E.Origin);
                statLine = String.Format("^5{0} ^7KILLS | ^5{1} ^7DEATHS | ^5{2} ^7KDR | ^5{3} ^7SKILL", pStats.Kills, pStats.Deaths, pStats.KDR, pStats.Skill);
            }

            if (E.Message.IsBroadcastCommand())
            {
                string name = E.Target == null ? E.Origin.Name : E.Target.Name;
                await E.Owner.Broadcast($"Stats for ^5{name}^7");
                await E.Owner.Broadcast(statLine);
            }

            else
            {
                if (E.Target != null)
                    await E.Origin.Tell($"Stats for ^5{E.Target.Name}^7");
                await E.Origin.Tell(statLine);
            }
        }
    }

    public class CViewTopStats : Command
    {
        public CViewTopStats() : base("topstats", "view the top 5 players on this server. syntax !topstats", "ts", Player.Permission.User, 0, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            List<KeyValuePair<String, PlayerStats>> pStats = Stats.statLists.Find(x => x.Port == E.Owner.GetPort()).playerStats.GetTopStats();
            StringBuilder msgBlder = new StringBuilder();

            await E.Origin.Tell("^5--Top Players--");
            foreach (KeyValuePair<String, PlayerStats> pStat in pStats)
            {
                Player P = E.Owner.Manager.GetClientDatabase().GetPlayer(pStat.Key, -1);
                if (P == null)
                    continue;
                await E.Origin.Tell(String.Format("^3{0}^7 - ^5{1} ^7KDR | ^5{2} ^7SKILL", P.Name, pStat.Value.KDR, pStat.Value.Skill));
            }
        }
    }


    public class CResetStats : Command
    {
        public CResetStats() : base("resetstats", "reset your stats to factory-new, !syntax !resetstats", "rs", Player.Permission.User, 0, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            var stats = Stats.statLists.Find(x => x.Port == E.Owner.GetPort()).playerStats.GetStats(E.Origin);
            stats.Deaths = 0;
            stats.Kills = 0;
            stats.scorePerMinute = 1.0;
            stats.Skill = 1;
            stats.KDR = 0.0;
            await Task.Run(() => { Stats.statLists.Find(x => x.Port == E.Owner.GetPort()).playerStats.UpdateStats(E.Origin, stats); });
            await E.Origin.Tell("Your stats have been reset");
        }
    }

    /// <summary>
    /// Each server runs from the same plugin ( for easier reloading and reduced memory usage ).
    /// So, to have multiple stat tracking, we must store a stat struct for each server
    /// </summary>
    public class Stats : SharedLibrary.Interfaces.IPlugin
    {
        private class KillEvent
        {
            public IW4Info.HitLocation HitLoc { get; set; }
            public IW4Info.MeansOfDeath DeathType { get; set; }
            public int Damage { get; set; }
            public IW4Info.WeaponName Weapon { get; set; }
            public Vector3 KillOrigin { get; set; }
            public Vector3 DeathOrigin { get; set; }

            public KillEvent(string hit, string type, string damage, string weapon, string kOrigin, string dOrigin)
            {
                HitLoc = (IW4Info.HitLocation)Enum.Parse(typeof(IW4Info.HitLocation), hit);
                DeathType = (IW4Info.MeansOfDeath)Enum.Parse(typeof(IW4Info.MeansOfDeath), type);
                Damage = Int32.Parse(damage);
                try
                {
                    Weapon = (IW4Info.WeaponName)Enum.Parse(typeof(IW4Info.WeaponName), weapon);
                }

                catch (Exception)
                {
                    Weapon = IW4Info.WeaponName.defaultweapon_mp;
                }

                KillOrigin = Vector3.Parse(kOrigin);
                DeathOrigin = Vector3.Parse(dOrigin);
            }
        }

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
            get { return 1.1f; }
        }

        public string Author
        {
            get { return "RaidMax"; }
        }

        public async Task OnLoadAsync()
        {
            statLists = new List<StatTracking>();
        }

        public async Task OnUnloadAsync()
        {
            statLists.Clear();
        }

        public async Task OnTickAsync(Server S)
        {
            return;
        }

        public async Task OnEventAsync(Event E, Server S)
        {
            if (E.Type == Event.GType.Start)
            {
                statLists.Add(new StatTracking(S.GetPort()));
                if (statLists.Count == 1)
                {
                    S.Manager.GetMessageTokens().Add(new SharedLibrary.Helpers.MessageToken("TOTALKILLS", GetTotalKills));
                    S.Manager.GetMessageTokens().Add(new SharedLibrary.Helpers.MessageToken("TOTALPLAYTIME", GetTotalPlaytime));
                }
            }

            if (E.Type == Event.GType.Stop)
            {
                statLists.RemoveAll(x => x.Port == S.GetPort());
            }

            if (E.Type == Event.GType.Connect)
            {
                ResetCounters(E.Origin.ClientID, S.GetPort());

                PlayerStats checkForTrusted = statLists.Find(x => x.Port == S.GetPort()).playerStats.GetStats(E.Origin);
                if (checkForTrusted.TotalPlayTime >= 4320 && E.Origin.Level < Player.Permission.Trusted)
                {
                    E.Origin.SetLevel(Player.Permission.Trusted);
                    E.Owner.Manager.GetClientDatabase().UpdatePlayer(E.Origin);
                    await E.Origin.Tell("Congratulations, you are now a ^5trusted ^7player! Type ^5!help ^7to view new commands.");
                    await E.Origin.Tell("You earned this by playing for ^53 ^7full days!");
                }
            }

            if (E.Type == Event.GType.MapEnd || E.Type == Event.GType.Stop)
            {
                foreach (Player P in S.GetPlayersAsList())
                {

                    if (P == null)
                        continue;

                    CalculateAndSaveSkill(P, statLists.Find(x => x.Port == S.GetPort()));
                    ResetCounters(P.ClientID, S.GetPort());

                    E.Owner.Logger.WriteInfo("Updated skill for client #" + P.DatabaseID);
                    //E.Owner.Log.Write(String.Format("\r\nJoin: {0}\r\nInactive Minutes: {1}\r\nnewPlayTime: {2}\r\nnewSPM: {3}\r\nkdrWeight: {4}\r\nMultiplier: {5}\r\nscoreWeight: {6}\r\nnewSkillFactor: {7}\r\nprojectedNewSkill: {8}\r\nKills: {9}\r\nDeaths: {10}", connectionTime[P.clientID].ToShortTimeString(), inactiveMinutes[P.clientID], newPlayTime, newSPM, kdrWeight, Multiplier, scoreWeight, newSkillFactor, disconnectStats.Skill, disconnectStats.Kills, disconnectStats.Deaths));
                }
            }

            if (E.Type == Event.GType.Disconnect)
            {
                CalculateAndSaveSkill(E.Origin, statLists.Find(x => x.Port == S.GetPort()));
                ResetCounters(E.Origin.ClientID, S.GetPort());
                E.Owner.Logger.WriteInfo("Updated skill for disconnecting client #" + E.Origin.DatabaseID);
            }

            if (E.Type == Event.GType.Kill)
            {
                if (E.Origin == E.Target || E.Origin == null)
                    return;

                string[] killInfo = E.Data.Split(';');

                var killEvent = new KillEvent(killInfo[7], killInfo[8], killInfo[5], killInfo[6], killInfo[3], killInfo[4]);

                S.Logger.WriteInfo($"{E.Origin.Name} killed {E.Target.Name} with a {killEvent.Weapon} from a distance of {Vector3.Distance(killEvent.KillOrigin, killEvent.DeathOrigin)} with {killEvent.Damage} damage, at {killEvent.HitLoc}");

                Player Killer = E.Origin;
                StatTracking curServer = statLists.Find(x => x.Port == S.GetPort());
                PlayerStats killerStats = curServer.playerStats.GetStats(Killer);


                curServer.lastKill[E.Origin.ClientID] = DateTime.Now;
                curServer.Kills[E.Origin.ClientID]++;

                if ((DateTime.Now - curServer.lastKill[E.Origin.ClientID]).TotalSeconds > 120)
                    curServer.inactiveMinutes[E.Origin.ClientID] += 2;

                killerStats.Kills++;

                killerStats.KDR = (killerStats.Deaths == 0) ? killerStats.Kills : killerStats.KDR = Math.Round((double)killerStats.Kills / (double)killerStats.Deaths, 2);


                curServer.playerStats.UpdateStats(Killer, killerStats);

                curServer.killStreaks[Killer.ClientID] += 1;
                curServer.deathStreaks[Killer.ClientID] = 0;

                await Killer.Tell(MessageOnStreak(curServer.killStreaks[Killer.ClientID], curServer.deathStreaks[Killer.ClientID]));
            }

            if (E.Type == Event.GType.Death)
            {
                if (E.Origin == E.Target || E.Origin == null)
                    return;

                Player Victim = E.Origin;
                StatTracking curServer = statLists.Find(x => x.Port == S.GetPort());
                PlayerStats victimStats = curServer.playerStats.GetStats(Victim);

                victimStats.Deaths++;
                victimStats.KDR = Math.Round(victimStats.Kills / (double)victimStats.Deaths, 2);

                curServer.playerStats.UpdateStats(Victim, victimStats);

                curServer.deathStreaks[Victim.ClientID] += 1;
                curServer.killStreaks[Victim.ClientID] = 0;

                await Victim.Tell(MessageOnStreak(curServer.killStreaks[Victim.ClientID], curServer.deathStreaks[Victim.ClientID]));
            }
        }

        public static string GetTotalKills()
        {
            long Kills = 0;
            foreach (var S in statLists)
                Kills += S.playerStats.GetTotalServerKills();
            return Kills.ToString("#,##0");
        }

        public static string GetTotalPlaytime()
        {
            long Playtime = 0;
            foreach (var S in statLists)
                Playtime += S.playerStats.GetTotalServerPlaytime();
            return Playtime.ToString("#,##0");
        }

        private void CalculateAndSaveSkill(Player P, StatTracking curServer)
        {
            if (P == null)
                return;

            PlayerStats DisconnectingPlayerStats = curServer.playerStats.GetStats(P);
            if (curServer.Kills[P.ClientID] == 0)
                return;

            else if (curServer.lastKill[P.ClientID] > curServer.connectionTime[P.ClientID])
                curServer.inactiveMinutes[P.ClientID] += (int)(DateTime.Now - curServer.lastKill[P.ClientID]).TotalMinutes;

            int newPlayTime = (int)(DateTime.Now - curServer.connectionTime[P.ClientID]).TotalMinutes - curServer.inactiveMinutes[P.ClientID];

            if (newPlayTime < 2)
                return;

            // calculate the players Score Per Minute for the current session
            double SessionSPM = curServer.Kills[P.ClientID] * 100 / Math.Max(1, newPlayTime);
            // calculate how much the KDR should way
            // 0.81829 is a Eddie-Generated number that weights the KDR nicely
            double KDRWeight = Math.Round(Math.Pow(DisconnectingPlayerStats.KDR, 1.637 / Math.E), 3);
            double SPMWeightAgainstAverage;

            // if no SPM, weight is 1 else the weight is the current sessions spm / lifetime average score per minute
            SPMWeightAgainstAverage = (DisconnectingPlayerStats.scorePerMinute == 1) ? 1 : SessionSPM / DisconnectingPlayerStats.scorePerMinute;

            // calculate the weight of the new play time againmst lifetime playtime
            // 
            double SPMAgainstPlayWeight = newPlayTime / Math.Min(600, DisconnectingPlayerStats.TotalPlayTime + newPlayTime);
            // calculate the new weight against average times the weight against play time
            double newSkillFactor = SPMWeightAgainstAverage * SPMAgainstPlayWeight * SessionSPM;

            // if the weight is greater than 1, add, else subtract
            DisconnectingPlayerStats.scorePerMinute += (SPMWeightAgainstAverage >= 1) ? newSkillFactor : -newSkillFactor;

            DisconnectingPlayerStats.Skill = DisconnectingPlayerStats.scorePerMinute * KDRWeight / 10;
            DisconnectingPlayerStats.TotalPlayTime += newPlayTime;

            curServer.playerStats.UpdateStats(P, DisconnectingPlayerStats);
        }

        private void ResetCounters(int cID, int serverPort)
        {
            StatTracking selectedPlayers = statLists.Find(x => x.Port == serverPort);

            selectedPlayers.Kills[cID] = 0;
            selectedPlayers.connectionTime[cID] = DateTime.Now;
            selectedPlayers.inactiveMinutes[cID] = 0;
            selectedPlayers.deathStreaks[cID] = 0;
            selectedPlayers.killStreaks[cID] = 0;
        }

        private String MessageOnStreak(int killStreak, int deathStreak)
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
                String createKillsTable = @"CREATE TABLE `KILLS` (
	                                                                `KillerID`	INTEGER NOT NULL,
	                                                                `VictimID`	INTEGER NOT NULL,
	                                                                `DeathOrigin`	TEXT NOT NULL,
	                                                                `MeansOfDeath`	INTEGER NOT NULL,
	                                                                 `Weapon`	INTEGER NOT NULL,
	                                                                 `HitLocation`	INTEGER NOT NULL,
	                                                                `Damage`	INTEGER,
	                                                                `KillOrigin`	TEXT NOT NULL
                                                                    ); ";
                ExecuteNonQuery(Create);
                ExecuteNonQuery(createKillsTable);
            }
        }

        public void AddPlayer(Player P)
        {
            Dictionary<String, object> newPlayer = new Dictionary<String, object>
            {
                { "npID", P.NetworkID },
                { "KILLS", 0 },
                { "DEATHS", 0 },
                { "KDR", 0.0 },
                { "SKILL", 1.0 },
                { "SPM", 1.0 },
                { "PLAYTIME", 1.0 }
            };
            Insert("STATS", newPlayer);
        }

        public PlayerStats GetStats(Player P)
        {
            DataTable Result = GetDataTable("STATS", new KeyValuePair<string, object>("npID", P.NetworkID));

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
                AddPlayer(P);
                return GetStats(P);
            }
        }

        public long GetTotalServerKills()
        {
            var Result = GetDataTable("SELECT SUM(KILLS) FROM STATS");
            return Result.Rows[0][0].GetType() == typeof(DBNull) ? 0 : Convert.ToInt64(Result.Rows[0][0]);
        }

        public long GetTotalServerPlaytime()
        {
            var Result = GetDataTable("SELECT SUM(PLAYTIME) FROM STATS");
            return Result.Rows[0][0].GetType() == typeof(DBNull) ? 0 : Convert.ToInt64(Result.Rows[0][0]) / 60;
        }

        public void UpdateStats(Player P, PlayerStats S)
        {
            Dictionary<String, object> updatedPlayer = new Dictionary<String, object>
            {
                { "KILLS", S.Kills },
                { "DEATHS", S.Deaths },
                { "KDR", Math.Round(S.KDR, 2) },
                { "SKILL", Math.Round(S.Skill, 2) },
                { "SPM", Math.Round(S.scorePerMinute, 2) },
                { "PLAYTIME", S.TotalPlayTime }
            };
            Update("STATS", updatedPlayer, new KeyValuePair<string, object>("npID", P.NetworkID));
        }

        public List<KeyValuePair<String, PlayerStats>> GetTopStats()
        {
            String Query = String.Format("SELECT * FROM STATS WHERE SKILL > 0 AND KDR < '{0}' AND KILLS > '{1}' AND PLAYTIME > '{2}' ORDER BY SKILL DESC LIMIT '{3}'", 10, 150, 60, 5);
            DataTable Result = GetDataTable(Query);
            List<KeyValuePair<String, PlayerStats>> pStats = new List<KeyValuePair<String, PlayerStats>>();

            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow ResponseRow in Result.Rows)
                {
                    pStats.Add(new KeyValuePair<String, PlayerStats>(ResponseRow["npID"].ToString(),
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
            TotalPlayTime = P;
        }

        public int Kills;
        public int Deaths;
        public double KDR;
        public double Skill;
        public double scorePerMinute;
        public int TotalPlayTime;
    }
}