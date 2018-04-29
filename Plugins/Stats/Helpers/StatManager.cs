using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using SharedLibraryCore;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;
using SharedLibraryCore.Commands;
using IW4MAdmin.Plugins.Stats.Models;

namespace IW4MAdmin.Plugins.Stats.Helpers
{
    public class StatManager
    {
        private ConcurrentDictionary<int, ServerStats> Servers;
        private ConcurrentDictionary<int, ThreadSafeStatsService> ContextThreads;
        private ILogger Log;
        private IManager Manager;

        public StatManager(IManager mgr)
        {
            Servers = new ConcurrentDictionary<int, ServerStats>();
            ContextThreads = new ConcurrentDictionary<int, ThreadSafeStatsService>();
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
                ContextThreads.TryAdd(serverId, statsSvc);

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
                Servers.TryAdd(serverId, new ServerStats(server, serverStats));
            }

            catch (Exception e)
            {
                Log.WriteError($"Could not add server to ServerStats - {e.Message}");
            }
        }

        /// <summary>
        /// Add Player to the player stats 
        /// </summary>
        /// <param name="pl">Player to add/retrieve stats for</param>
        /// <returns>EFClientStatistic of specified player</returns>
        public async Task<EFClientStatistics> AddPlayer(Player pl)
        {
            int serverId = pl.CurrentServer.GetHashCode();

            if (!Servers.ContainsKey(serverId))
            {
                Log.WriteError($"[Stats::AddPlayer] Server with id {serverId} could not be found");
                return null;
            }

            var playerStats = Servers[serverId].PlayerStats;
            var statsSvc = ContextThreads[serverId];
            var detectionStats = Servers[serverId].PlayerDetections;

            if (playerStats.ContainsKey(pl.ClientId))
            {
                Log.WriteWarning($"Duplicate ClientId in stats {pl.ClientId}");
                return null;
            }

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
                    HitLocations = Enum.GetValues(typeof(IW4Info.HitLocation)).OfType<IW4Info.HitLocation>().Select(hl => new EFHitLocationCount()
                    {
                        Active = true,
                        HitCount = 0,
                        Location = hl
                    })
                    .ToList()
                };

                clientStats = statsSvc.ClientStatSvc.Insert(clientStats);
                await statsSvc.ClientStatSvc.SaveChangesAsync();
            }

            // migration for previous existing stats
            else if (clientStats.HitLocations.Count == 0)
            {
                clientStats.HitLocations = Enum.GetValues(typeof(IW4Info.HitLocation)).OfType<IW4Info.HitLocation>().Select(hl => new EFHitLocationCount()
                {
                    Active = true,
                    HitCount = 0,
                    Location = hl
                })
                .ToList();
                await statsSvc.ClientStatSvc.SaveChangesAsync();
            }

            // set these on connecting
            clientStats.LastActive = DateTime.UtcNow;
            clientStats.LastStatCalculation = DateTime.UtcNow;
            clientStats.SessionScore = pl.Score;

            Log.WriteInfo($"Adding {pl} to stats");

            if (!playerStats.TryAdd(pl.ClientId, clientStats))
                Log.WriteDebug($"Could not add client to stats {pl}");

            if (!detectionStats.TryAdd(pl.ClientId, new Cheat.Detection(Log, clientStats)))
                Log.WriteDebug("Could not add client to detection");


            // todo: look at this more
            statsSvc.ClientStatSvc.Update(clientStats);
            await statsSvc.ClientStatSvc.SaveChangesAsync();

            return clientStats;
        }

        /// <summary>
        /// Perform stat updates for disconnecting client
        /// </summary>
        /// <param name="pl">Disconnecting client</param>
        /// <returns></returns>
        public async Task RemovePlayer(Player pl)
        {
            Log.WriteInfo($"Removing {pl} from stats");

            int serverId = pl.CurrentServer.GetHashCode();
            var playerStats = Servers[serverId].PlayerStats;
            var detectionStats = Servers[serverId].PlayerDetections;
            var serverStats = Servers[serverId].ServerStatistics;
            var statsSvc = ContextThreads[serverId];

            if (!playerStats.ContainsKey(pl.ClientId))
            {
                Log.WriteWarning($"Client disconnecting not in stats {pl}");
                // remove the client from the stats dictionary as they're leaving
                playerStats.TryRemove(pl.ClientId, out EFClientStatistics removedValue1);
                detectionStats.TryRemove(pl.ClientId, out Cheat.Detection removedValue2);
                return;
            }

            // get individual client's stats
            var clientStats = playerStats[pl.ClientId];
            /*// sync their score
            clientStats.SessionScore += pl.Score;*/

            // remove the client from the stats dictionary as they're leaving
            playerStats.TryRemove(pl.ClientId, out EFClientStatistics removedValue3);
            detectionStats.TryRemove(pl.ClientId, out Cheat.Detection removedValue4);

           /* // sync their stats before they leave
            clientStats = UpdateStats(clientStats);*/

            // todo: should this be saved every disconnect?
            statsSvc.ClientStatSvc.Update(clientStats);
            await statsSvc.ClientStatSvc.SaveChangesAsync();
            // increment the total play time
            serverStats.TotalPlayTime += (int)(DateTime.UtcNow - pl.LastConnection).TotalSeconds;
            await statsSvc.ServerStatsSvc.SaveChangesAsync();
        }

        /// <summary>
        /// Process stats for kill event
        /// </summary>
        /// <returns></returns>
        public async Task AddScriptKill(Player attacker, Player victim, int serverId, string map, string hitLoc, string type,
            string damage, string weapon, string killOrigin, string deathOrigin, string viewAngles, string offset, string isKillstreakKill, string Ads)
        {
            var statsSvc = ContextThreads[serverId];
            Vector3 vDeathOrigin = null;
            Vector3 vKillOrigin = null;
            Vector3 vViewAngles = null;

            try
            {
                vDeathOrigin = Vector3.Parse(deathOrigin);
                vKillOrigin = Vector3.Parse(killOrigin);
                vViewAngles = Vector3.Parse(viewAngles).FixIW4Angles();
            }

            catch (FormatException)
            {
                Log.WriteWarning("Could not parse kill or death origin or viewangle vectors");
                Log.WriteDebug($"Kill - {killOrigin} Death - {deathOrigin} ViewAngle - {viewAngles}");
                await AddStandardKill(attacker, victim);
                return;
            }

            var kill = new EFClientKill()
            {
                Active = true,
                AttackerId = attacker.ClientId,
                VictimId = victim.ClientId,
                ServerId = serverId,
                Map = ParseEnum<IW4Info.MapName>.Get(map, typeof(IW4Info.MapName)),
                DeathOrigin = vDeathOrigin,
                KillOrigin = vKillOrigin,
                DeathType = ParseEnum<IW4Info.MeansOfDeath>.Get(type, typeof(IW4Info.MeansOfDeath)),
                Damage = Int32.Parse(damage),
                HitLoc = ParseEnum<IW4Info.HitLocation>.Get(hitLoc, typeof(IW4Info.HitLocation)),
                Weapon = ParseEnum<IW4Info.WeaponName>.Get(weapon, typeof(IW4Info.WeaponName)),
                ViewAngles = vViewAngles,
                TimeOffset = Int64.Parse(offset),
                When = DateTime.UtcNow,
                IsKillstreakKill = isKillstreakKill[0] != '0',
                AdsPercent = float.Parse(Ads)
            };

            if (kill.DeathType == IW4Info.MeansOfDeath.MOD_SUICIDE &&
                kill.Damage == 100000)
            {
                // suicide by switching teams so let's not count it against them
                return;
            }

            await AddStandardKill(attacker, victim);

            if (kill.IsKillstreakKill)
            {
                return;
            }

            var clientDetection = Servers[serverId].PlayerDetections[attacker.ClientId];
            var clientStats = Servers[serverId].PlayerStats[attacker.ClientId];

            // increment their hit count
            if (kill.DeathType == IW4Info.MeansOfDeath.MOD_PISTOL_BULLET ||
                kill.DeathType == IW4Info.MeansOfDeath.MOD_RIFLE_BULLET ||
                kill.DeathType == IW4Info.MeansOfDeath.MOD_HEAD_SHOT)
            {
                clientStats.HitLocations.Single(hl => hl.Location == kill.HitLoc).HitCount += 1;

                statsSvc.ClientStatSvc.Update(clientStats);
                await statsSvc.ClientStatSvc.SaveChangesAsync();
            }

            //statsSvc.KillStatsSvc.Insert(kill);
            //await statsSvc.KillStatsSvc.SaveChangesAsync();

            if (Plugin.Config.Configuration().EnableAntiCheat)
            {
                async Task executePenalty(Cheat.DetectionPenaltyResult penalty)
                {
                    switch (penalty.ClientPenalty)
                    {
                        case Penalty.PenaltyType.Ban:
                            await attacker.Ban("You appear to be cheating", new Player() { ClientId = 1 });
                            break;
                        case Penalty.PenaltyType.Flag:
                            if (attacker.Level != Player.Permission.User)
                                break;
                            var flagCmd = new CFlag();
                            await flagCmd.ExecuteAsync(new GameEvent(GameEvent.EventType.Flag, $"{(int)penalty.Bone}-{Math.Round(penalty.RatioAmount, 2).ToString()}@{penalty.KillCount}", new Player()
                            {
                                ClientId = 1,
                                Level = Player.Permission.Console,
                                ClientNumber = -1,
                                CurrentServer = attacker.CurrentServer
                            }, attacker, attacker.CurrentServer));
                            break;
                    }
                }

                await executePenalty(clientDetection.ProcessKill(kill));
                await executePenalty(clientDetection.ProcessTotalRatio(clientStats));
            }
        }

        public async Task AddStandardKill(Player attacker, Player victim)
        {
            int serverId = attacker.CurrentServer.GetHashCode();
            EFClientStatistics attackerStats = null;
            try
            {
                attackerStats = Servers[serverId].PlayerStats[attacker.ClientId];
            }

            catch (KeyNotFoundException)
            {
                Log.WriteError($"[Stats::AddStandardKill] kill attacker ClientId is invalid {attacker.ClientId}-{attacker}");
                return;
            }

            EFClientStatistics victimStats = null;
            try
            {
                victimStats = Servers[serverId].PlayerStats[victim.ClientId];
            }

            catch (KeyNotFoundException)
            {
                Log.WriteError($"[Stats::AddStandardKill] kill victim ClientId is invalid {victim.ClientId}-{victim}");
                return;
            }

#if DEBUG
            Log.WriteDebug("Calculating standard kill");
#endif

            // update the total stats
            Servers[serverId].ServerStatistics.TotalKills += 1;

            // this happens when the round has changed
            if (attackerStats.SessionScore == 0)
                attackerStats.LastScore = 0;

            if (victimStats.SessionScore == 0)
                victimStats.LastScore = 0;

            attackerStats.SessionScore = attacker.Score;
            victimStats.SessionScore = victim.Score;

            // calculate for the clients
            CalculateKill(attackerStats, victimStats);
            // this should fix the negative SPM
            // updates their last score after being calculated
            attackerStats.LastScore = attacker.Score;
            victimStats.LastScore = victim.Score;

            // show encouragement/discouragement
            string streakMessage = (attackerStats.ClientId != victimStats.ClientId) ?
                StreakMessage.MessageOnStreak(attackerStats.KillStreak, attackerStats.DeathStreak) :
                StreakMessage.MessageOnStreak(-1, -1);

            if (streakMessage != string.Empty)
                await attacker.Tell(streakMessage);

            // fixme: why?
            if (double.IsNaN(victimStats.SPM) || double.IsNaN(victimStats.Skill))
            {
                Log.WriteDebug($"[StatManager::AddStandardKill] victim SPM/SKILL {victimStats.SPM} {victimStats.Skill}");
                victimStats.SPM = 0.0;
                victimStats.Skill = 0.0;
            }

            if (double.IsNaN(attackerStats.SPM) || double.IsNaN(attackerStats.Skill))
            {
                Log.WriteDebug($"[StatManager::AddStandardKill] attacker SPM/SKILL {victimStats.SPM} {victimStats.Skill}");
                attackerStats.SPM = 0.0;
                attackerStats.Skill = 0.0;
            }

            // todo: do we want to save this immediately?
            var statsSvc = ContextThreads[serverId];
            statsSvc.ClientStatSvc.Update(attackerStats);
            statsSvc.ClientStatSvc.Update(victimStats);
            await statsSvc.ClientStatSvc.SaveChangesAsync();
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
            attackerStats = UpdateStats(attackerStats);

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
            // prevent NaN or inactive time lowering SPM
            if ((DateTime.UtcNow - clientStats.LastStatCalculation).TotalSeconds / 60.0 < 0.01 ||
                (DateTime.UtcNow - clientStats.LastActive).TotalSeconds / 60.0 > 3 ||
                clientStats.SessionScore == 0)
            {
                // prevents idle time counting
                clientStats.LastStatCalculation = DateTime.UtcNow;
                return clientStats;
            }

            double timeSinceLastCalc = (DateTime.UtcNow - clientStats.LastStatCalculation).TotalSeconds / 60.0;
            double timeSinceLastActive = (DateTime.UtcNow - clientStats.LastActive).TotalSeconds / 60.0;

            // calculate the players Score Per Minute for the current session
            int scoreDifference = clientStats.RoundScore - clientStats.LastScore;
            double killSPM = scoreDifference / timeSinceLastCalc;

            // calculate how much the KDR should weigh
            // 1.637 is a Eddie-Generated number that weights the KDR nicely
            double kdr = clientStats.Deaths == 0 ? clientStats.Kills : clientStats.KDR;
            double KDRWeight = Math.Round(Math.Pow(kdr, 1.637 / Math.E), 3);

            // calculate the weight of the new play time against last 10 hours of gameplay
            int totalPlayTime = (clientStats.TimePlayed == 0) ?
                (int)(DateTime.UtcNow - clientStats.LastActive).TotalSeconds :
                clientStats.TimePlayed + (int)(DateTime.UtcNow - clientStats.LastActive).TotalSeconds;

            double SPMAgainstPlayWeight = timeSinceLastCalc / Math.Min(600, (totalPlayTime / 60.0));

            // calculate the new weight against average times the weight against play time
            clientStats.SPM = (killSPM * SPMAgainstPlayWeight) + (clientStats.SPM * (1 - SPMAgainstPlayWeight));

            if (clientStats.SPM < 0)
            {
                Log.WriteWarning("[StatManager:UpdateStats] clientStats SPM < 0");
                Log.WriteDebug($"{scoreDifference}-{clientStats.RoundScore} - {clientStats.LastScore} - {clientStats.SessionScore}");
                clientStats.SPM = 0;
            }

            clientStats.SPM = Math.Round(clientStats.SPM, 3);
            clientStats.Skill = Math.Round((clientStats.SPM * KDRWeight), 3);

            // fixme: how does this happen?
            if (double.IsNaN(clientStats.SPM) || double.IsNaN(clientStats.Skill))
            {
                Log.WriteWarning("[StatManager::UpdateStats] clientStats SPM/Skill NaN");
                Log.WriteDebug($"{killSPM}-{KDRWeight}-{totalPlayTime}-{SPMAgainstPlayWeight}-{clientStats.SPM}-{clientStats.Skill}-{scoreDifference}");
                clientStats.SPM = 0;
                clientStats.Skill = 0;
            }

            clientStats.LastStatCalculation = DateTime.UtcNow;
            //clientStats.LastScore = clientStats.SessionScore;

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

        public void ResetKillstreaks(int serverId)
        {
            var serverStats = Servers[serverId];
            foreach (var stat in serverStats.PlayerStats.Values)
                stat.StartNewSession();
        }

        public void ResetStats(int clientId, int serverId)
        {
            var stats = Servers[serverId].PlayerStats[clientId];
            stats.Kills = 0;
            stats.Deaths = 0;
            stats.SPM = 0;
            stats.Skill = 0;
            stats.TimePlayed = 0;
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
