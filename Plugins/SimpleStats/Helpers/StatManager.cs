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

            else

            lock (playerStats)
                playerStats.Add(pl.ClientNumber, clientStats);
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

            // update in database
            await ClientStatSvc.SaveChangesAsync();
        }

        /// <summary>
        /// Process stats for kill event
        /// </summary>
        /// <returns></returns>
        public async Task AddKill(Player attacker, Player victim, int serverId, string map, string hitLoc, string type, 
            string damage, string weapon, string killOrigin, string deathOrigin)
        {
            var attackerStats = Servers[serverId].PlayerStats[attacker.ClientNumber];
            attackerStats.Kills += 1;

            var victimStats = Servers[serverId].PlayerStats[victim.ClientNumber];
            victimStats.Deaths += 1;

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

        private EFClientStatistics UpdateStats(EFClientStatistics cs)
        {
            // todo: everything
            return cs;
        }
    }
}
