using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private ILogger Log;
        private IManager Manager;
        private GenericRepository<EFClientStatistics> ClientStatSvc;
        private GenericRepository<EFServer> ServerSvc;
        private GenericRepository<EFClientKill> KillSvc;

        public StatManager(IManager mgr)
        {
            Servers = new Dictionary<int, ServerStats>();
            Log = mgr.GetLogger();
            Manager = mgr;
            ClientStatSvc = new GenericRepository<EFClientStatistics>();
            ServerSvc = new GenericRepository<EFServer>();
            KillSvc = new GenericRepository<EFClientKill>();
        }

        ~StatManager()
        {
            Servers.Clear();
            Log.WriteInfo("Cleared StatManager servers");
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
                // get the server from the database if it exists, otherwise create and insert a new one
                var server = ServerSvc.Find(c => c.ServerId == serverId).FirstOrDefault();
                if (server == null)
                {
                    server = new EFServer()
                    {
                        Port = sv.GetPort(),
                        Active = true,
                        ServerId = serverId
                    };

                    ServerSvc.Insert(server);
                }

                // this doesn't need to be async as it's during initialization
                ServerSvc.SaveChanges();
                Servers.Add(sv.GetHashCode(), new ServerStats(server));
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

            // get the client's stats from the database if it exists, otherwise create and attach a new one
            // if this fails we want to throw an exception
            var clientStats = ClientStatSvc.Find(c => c.ClientId == pl.ClientId && c.ServerId == serverId).FirstOrDefault();
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

                clientStats = ClientStatSvc.Insert(clientStats);
            }

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

        public async Task RemovePlayer(Player pl)
        {
            int serverId = pl.CurrentServer.GetHashCode();
            var playerStats = Servers[serverId].PlayerStats;
            // get individual client's stats
            var clientStats = playerStats[pl.ClientNumber];
            // remove the client from the stats dictionary as they're leaving
            lock (playerStats)
                playerStats.Remove(pl.ClientNumber);
            // allow accessing certain properties
            //clientStats.Client = pl;
            // update skill
            //  clientStats = UpdateStats(clientStats);
            // reset for EF cache
            //clientStats.SessionDeaths = 0;
            //  clientStats.SessionKills = 0;
            // prevent mismatched primary key
            //clientStats.Client = null;
            // update in database
            //await ClientStatSvc.SaveChangesAsync();
        }

        /// <summary>
        /// Process stats for kill event
        /// </summary>
        /// <returns></returns>
        public async Task AddScriptKill(Player attacker, Player victim, int serverId, string map, string hitLoc, string type,
            string damage, string weapon, string killOrigin, string deathOrigin)
        {
            AddStandardKill(attacker, victim);

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

            KillSvc.Insert(kill);
            await KillSvc.SaveChangesAsync();
        }

        public void AddStandardKill(Player attacker, Player victim)
        {
            var attackerStats = Servers[attacker.CurrentServer.GetHashCode()].PlayerStats[attacker.ClientNumber];
            // set to access total time
            attackerStats.Client = attacker;
            var victimStats = Servers[victim.CurrentServer.GetHashCode()].PlayerStats[victim.ClientNumber];

            CalculateKill(attackerStats, victimStats);
        }

        /// <summary>
        /// Performs the incrementation of kills and deaths for client statistics
        /// </summary>
        /// <param name="attackerStats">Stats of the attacker</param>
        /// <param name="victimStats">Stats of the victim</param>
        public void CalculateKill(EFClientStatistics attackerStats, EFClientStatistics victimStats)
        {
            attackerStats.Kills += 1;
            attackerStats.SessionKills += 1;
            attackerStats.KillStreak += 1;
            attackerStats.DeathStreak = 0;

            victimStats.Deaths += 1;
            victimStats.SessionDeaths += 1;
            victimStats.DeathStreak += 1;
            victimStats.KillStreak = 0;

            // process the attacker's stats after the kills
            UpdateStats(attackerStats);
            attackerStats.Client = null;

            // immediately write changes in debug
#if DEBUG
            ClientStatSvc.SaveChanges();
#endif
        }

        /// <summary>
        /// Update the client stats (skill etc)
        /// </summary>
        /// <param name="clientStats">Client statistics</param>
        /// <returns></returns>
        private EFClientStatistics UpdateStats(EFClientStatistics clientStats)
        {
            // if it's their first kill we need to set the last kill as the time they joined 
            clientStats.LastStatCalculation = (clientStats.LastStatCalculation == DateTime.MinValue) ? DateTime.UtcNow : clientStats.LastStatCalculation;
            double timeSinceLastCalc = (DateTime.UtcNow - clientStats.LastStatCalculation).TotalSeconds / 60.0;

            // each 'session' is one minute
            if (timeSinceLastCalc >= 1)
            {
                Log.WriteDebug($"Updated stats for {clientStats.ClientId} ({clientStats.SessionKills})");
                // calculate the players Score Per Minute for the current session
                // todo: score should be based on gamemode
                double killSPM = clientStats.SessionKills * 100.0;

                // calculate how much the KDR should weigh
                // 1.637 is a Eddie-Generated number that weights the KDR nicely
                double KDRWeight = Math.Round(Math.Pow(clientStats.KDR, 1.637 / Math.E), 3);

                // if no SPM, weight is 1 else the weight ishe current session's spm / lifetime average score per minute
                double SPMWeightAgainstAverage = (clientStats.SPM < 1) ? 1 : killSPM / clientStats.SPM;

                // calculate the weight of the new play time against last 10 hours of gameplay
                int totalConnectionTime = (clientStats.Client.TotalConnectionTime == 0) ? 
                    (int)(DateTime.UtcNow - clientStats.Client.FirstConnection).TotalSeconds : 
                    clientStats.Client.TotalConnectionTime + (int)(DateTime.UtcNow - clientStats.Client.LastConnection).TotalSeconds;

                double SPMAgainstPlayWeight =  timeSinceLastCalc / Math.Min(600, (totalConnectionTime / 60.0));

                // calculate the new weight against average times the weight against play time
                clientStats.SPM = (killSPM * SPMAgainstPlayWeight) + (clientStats.SPM * (1 - SPMAgainstPlayWeight));
                clientStats.SPM = Math.Round(clientStats.SPM, 3);
                clientStats.Skill = Math.Round((clientStats.SPM * KDRWeight) / 10.0, 3);

                clientStats.SessionKills = 0;
                clientStats.SessionDeaths = 0;

                clientStats.LastStatCalculation = DateTime.UtcNow;
            }

            return clientStats;
        }
    }
}
