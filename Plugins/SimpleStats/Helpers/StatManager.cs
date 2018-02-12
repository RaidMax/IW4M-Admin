using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SharedLibrary;
using SharedLibrary.Helpers;
using SharedLibrary.Interfaces;
using SharedLibrary.Objects;
using SharedLibrary.Services;
using StatsPlugin.Models;

namespace StatsPlugin.Helpers
{
    public class StatManager
    {
        private Dictionary<int, ServerStats> Servers;
        private Dictionary<int, ThreadSafeStatsService> ContextThreads;
        private Dictionary<int, StreakMessage> StreakMessages;
        private ILogger Log;
        private IManager Manager;


        public StatManager(IManager mgr)
        {
            Servers = new Dictionary<int, ServerStats>();
            ContextThreads = new Dictionary<int, ThreadSafeStatsService>();
            StreakMessages= new Dictionary<int, StreakMessage>();
            Log = mgr.GetLogger();
            Manager = mgr;
        }

        ~StatManager()
        {
            Servers.Clear();
            Log = null;
            Servers = null;
        }

        /// <summary>
        /// Add a server to the StatManager server pool
        /// </summary>
        /// <param name="sv"></param>
        public void AddServer(Server sv)
        {
            try
            {
                int serverId = sv.GetHashCode();
                var statsSvc = new ThreadSafeStatsService();
                ContextThreads.Add(serverId, statsSvc);
                StreakMessages.Add(serverId, new StreakMessage(sv));

                // get the server from the database if it exists, otherwise create and insert a new one
                var server = statsSvc.ServerSvc.Find(c => c.ServerId == serverId).FirstOrDefault();
                if (server == null)
                {
                    server = new EFServer()
                    {
                        Port = sv.GetPort(),
                        Active = true,
                        ServerId = serverId
                    };

                    statsSvc.ServerSvc.Insert(server);
                }

                // this doesn't need to be async as it's during initialization
                statsSvc.ServerSvc.SaveChanges();
                // check to see if the stats have ever been initialized
                InitializeServerStats(sv);
                statsSvc.ServerStatsSvc.SaveChanges();

                var serverStats = statsSvc.ServerStatsSvc.Find(c => c.ServerId == serverId).FirstOrDefault();
                Servers.Add(serverId, new ServerStats(server, serverStats));
            }

            catch (Exception e)
            {
                Log.WriteWarning($"Could not add server to ServerStats - {e.Message}");
            }
        }

        /// <summary>
        /// Add Player to the player stats 
        /// </summary>
        /// <param name="pl">Player to add/retrieve stats for</param>
        /// <returns>EFClientStatistic of specified player</returns>
        public EFClientStatistics AddPlayer(Player pl)
        {
            int serverId = pl.CurrentServer.GetHashCode();
            var playerStats = Servers[serverId].PlayerStats;
            var statsSvc = ContextThreads[serverId];

            // get the client's stats from the database if it exists, otherwise create and attach a new one
            // if this fails we want to throw an exception
            var clientStats = statsSvc.ClientStatSvc.Find(c => c.ClientId == pl.ClientId && c.ServerId == serverId).FirstOrDefault();
            if (clientStats == null)
            {
                clientStats = new EFClientStatistics()
                {
                    Active = true,
                    ClientId = pl.ClientId,
                    Deaths = 0,
                    Kills = 0,
                    ServerId = serverId,
                    Skill = 0.0,
                    SPM = 0.0,
                };

                clientStats = statsSvc.ClientStatSvc.Insert(clientStats);
            }

            // set these on connecting
            clientStats.LastActive = DateTime.UtcNow;
            clientStats.LastStatCalculation = DateTime.UtcNow;

            lock (playerStats)
            {
                if (playerStats.ContainsKey(pl.ClientNumber))
                {
                    Log.WriteWarning($"Duplicate clientnumber in stats {pl.ClientId} vs {playerStats[pl.ClientNumber].ClientId}");
                    playerStats.Remove(pl.ClientNumber);
                }
                playerStats.Add(pl.ClientNumber, clientStats);
            }
            return clientStats;
        }

        /// <summary>
        /// Perform stat updates for disconnecting client
        /// </summary>
        /// <param name="pl">Disconnecting client</param>
        /// <returns></returns>
        public async Task RemovePlayer(Player pl)
        {
            int serverId = pl.CurrentServer.GetHashCode();
            var playerStats = Servers[serverId].PlayerStats;
            var serverStats = Servers[serverId].ServerStatistics;
            var statsSvc = ContextThreads[serverId];

            // get individual client's stats
            var clientStats = playerStats[pl.ClientNumber];
            // remove the client from the stats dictionary as they're leaving
            lock (playerStats)
                playerStats.Remove(pl.ClientNumber);

            // sync their stats before they leave
            clientStats.Client = pl;
            UpdateStats(clientStats);
            clientStats.Client = null;

            // todo: should this be saved every disconnect?
            await statsSvc.ClientStatSvc.SaveChangesAsync();
            // increment the total play time
            serverStats.TotalPlayTime += (int)(DateTime.UtcNow - pl.LastConnection).TotalSeconds;
        }

        /// <summary>
        /// Process stats for kill event
        /// </summary>
        /// <returns></returns>
        public async Task AddScriptKill(Player attacker, Player victim, int serverId, string map, string hitLoc, string type,
            string damage, string weapon, string killOrigin, string deathOrigin)
        {
            await AddStandardKill(attacker, victim);

            var statsSvc = ContextThreads[serverId];

            var kill = new EFClientKill()
            {
                Active = true,
                AttackerId = attacker.ClientId,
                VictimId = victim.ClientId,
                ServerId = serverId,
                Map = ParseEnum<IW4Info.MapName>.Get(map, typeof(IW4Info.MapName)),
                DeathOrigin = Vector3.Parse(deathOrigin),
                KillOrigin = Vector3.Parse(killOrigin),
                DeathType = ParseEnum<IW4Info.MeansOfDeath>.Get(type, typeof(IW4Info.MeansOfDeath)),
                Damage = Int32.Parse(damage),
                HitLoc = ParseEnum<IW4Info.HitLocation>.Get(hitLoc, typeof(IW4Info.HitLocation)),
                Weapon = ParseEnum<IW4Info.WeaponName>.Get(weapon, typeof(IW4Info.WeaponName))
            };

            statsSvc.KillStatsSvc.Insert(kill);
            await statsSvc.KillStatsSvc.SaveChangesAsync();
        }

        public async Task AddStandardKill(Player attacker, Player victim)
        {
            int serverId = attacker.CurrentServer.GetHashCode();
            var attackerStats = Servers[serverId].PlayerStats[attacker.ClientNumber];
            // set to access total time played
            attackerStats.Client = attacker;

            if (victim == null)
            {
                Log.WriteError($"Stats: Victim is null");
                return;
            }

            var victimStats = Servers[serverId].PlayerStats[victim.ClientNumber];

            // update the total stats
            Servers[serverId].ServerStatistics.TotalKills += 1;

            // calculate for the clients
            CalculateKill(attackerStats, victimStats);

            // show encouragement/discouragement
            var streakMessageGen = StreakMessages[serverId];
            string streakMessage =  (attackerStats.ClientId != victimStats.ClientId) ?
                streakMessageGen.MessageOnStreak(attackerStats.KillStreak, attackerStats.DeathStreak) :
                streakMessageGen.MessageOnStreak(-1, -1);

            if (streakMessage != string.Empty)
                await attacker.Tell(streakMessage);

            // immediately write changes in debug
#if DEBUG
            var statsSvc = ContextThreads[serverId];
            statsSvc.ClientStatSvc.SaveChanges();
            statsSvc.ServerStatsSvc.SaveChanges();
#endif
        }

        /// <summary>
        /// Performs the incrementation of kills and deaths for client statistics
        /// </summary>
        /// <param name="attackerStats">Stats of the attacker</param>
        /// <param name="victimStats">Stats of the victim</param>
        public void CalculateKill(EFClientStatistics attackerStats, EFClientStatistics victimStats)
        {
            bool suicide = attackerStats.ClientId == victimStats.ClientId;

            // only update their kills if they didn't kill themselves
            if (!suicide)
            {
                attackerStats.Kills += 1;
                attackerStats.SessionKills += 1;
                attackerStats.KillStreak += 1;
                attackerStats.DeathStreak = 0;
            }

            victimStats.Deaths += 1;
            victimStats.SessionDeaths += 1;
            victimStats.DeathStreak += 1;
            victimStats.KillStreak = 0;

            // process the attacker's stats after the kills
            UpdateStats(attackerStats);
            attackerStats.Client = null;

            // update after calculation
            attackerStats.TimePlayed += (int)(DateTime.UtcNow - attackerStats.LastActive).TotalSeconds;
            victimStats.TimePlayed += (int)(DateTime.UtcNow - victimStats.LastActive).TotalSeconds;
            attackerStats.LastActive = DateTime.UtcNow;
            victimStats.LastActive = DateTime.UtcNow;
        }

        /// <summary>
        /// Update the client stats (skill etc)
        /// </summary>
        /// <param name="clientStats">Client statistics</param>
        /// <returns></returns>
        private EFClientStatistics UpdateStats(EFClientStatistics clientStats)
        {
            double timeSinceLastCalc = (DateTime.UtcNow - clientStats.LastStatCalculation).TotalSeconds / 60.0;
            double timeSinceLastActive = (DateTime.UtcNow - clientStats.LastActive).TotalSeconds / 60.0;

            // prevent NaN or inactive time lowering SPM
            if (timeSinceLastCalc == 0 || timeSinceLastActive > 3)
                return clientStats;

            // calculate the players Score Per Minute for the current session
            int currentScore = Manager.GetActiveClients()
                .First(c => c.ClientId == clientStats.ClientId)
                .Score;
            double killSPM = currentScore / (timeSinceLastCalc * 60.0);

            // calculate how much the KDR should weigh
            // 1.637 is a Eddie-Generated number that weights the KDR nicely
            double KDRWeight = Math.Round(Math.Pow(clientStats.KDR, 1.637 / Math.E), 3);

            // if no SPM, weight is 1 else the weight ishe current session's spm / lifetime average score per minute
            double SPMWeightAgainstAverage = (clientStats.SPM < 1) ? 1 : killSPM / clientStats.SPM;

            // calculate the weight of the new play time against last 10 hours of gameplay
            int totalConnectionTime = (clientStats.Client.TotalConnectionTime == 0) ?
                (int)(DateTime.UtcNow - clientStats.Client.FirstConnection).TotalSeconds :
                clientStats.Client.TotalConnectionTime + (int)(DateTime.UtcNow - clientStats.Client.LastConnection).TotalSeconds;

            double SPMAgainstPlayWeight = timeSinceLastCalc / Math.Min(600, (totalConnectionTime / 60.0));

            // calculate the new weight against average times the weight against play time
            clientStats.SPM = (killSPM * SPMAgainstPlayWeight) + (clientStats.SPM * (1 - SPMAgainstPlayWeight));
            clientStats.SPM = Math.Round(clientStats.SPM, 3);
            clientStats.Skill = Math.Round((clientStats.SPM * KDRWeight), 3);

            clientStats.LastStatCalculation = DateTime.UtcNow;
            clientStats.LastScore = currentScore;

            return clientStats;
        }

        public void InitializeServerStats(Server sv)
        {
            int serverId = sv.GetHashCode();
            var statsSvc = ContextThreads[serverId];

            var serverStats = statsSvc.ServerStatsSvc.Find(s => s.ServerId == serverId).FirstOrDefault();
            if (serverStats == null)
            {
                Log.WriteDebug($"Initializing server stats for {sv}");
                // server stats have never been generated before
                serverStats = new EFServerStatistics()
                {
                    Active = true,
                    ServerId = serverId,
                    TotalKills = 0,
                    TotalPlayTime = 0,
                };

                var ieClientStats = statsSvc.ClientStatSvc.Find(cs => cs.ServerId == serverId);

                // set these incase they've we've imported settings 
                serverStats.TotalKills = ieClientStats.Sum(cs => cs.Kills);
                serverStats.TotalPlayTime = Manager.GetClientService().GetTotalPlayTime().Result;

                statsSvc.ServerStatsSvc.Insert(serverStats);
            }
        }

        public async Task AddMessageAsync(int clientId, int serverId, string message)
        {
            // the web users can have no account
            if (clientId < 1)
                return;

            var messageSvc = ContextThreads[serverId].MessageSvc;
            messageSvc.Insert(new EFClientMessage()
            {
                Active = true,
                ClientId = clientId,
                Message = message,
                ServerId = serverId,
                TimeSent = DateTime.UtcNow
            });
            await messageSvc.SaveChangesAsync();
        }

        public async Task Sync(Server sv)
        {
            int serverId = sv.GetHashCode();
            var statsSvc = ContextThreads[serverId];

            Log.WriteDebug("Syncing server stats");
            await statsSvc.ServerStatsSvc.SaveChangesAsync();

            Log.WriteDebug("Syncing client stats");
            await statsSvc.ClientStatSvc.SaveChangesAsync();

            Log.WriteDebug("Syncing kill stats");
            await statsSvc.KillStatsSvc.SaveChangesAsync();

            Log.WriteDebug("Syncing servers");
            await statsSvc.ServerSvc.SaveChangesAsync();
        }
    }
}
