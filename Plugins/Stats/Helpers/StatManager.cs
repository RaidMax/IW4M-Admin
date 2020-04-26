using IW4MAdmin.Plugins.Stats.Cheat;
using IW4MAdmin.Plugins.Stats.Config;
using IW4MAdmin.Plugins.Stats.Models;
using IW4MAdmin.Plugins.Stats.Web.Dtos;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using static IW4MAdmin.Plugins.Stats.Cheat.Detection;

namespace IW4MAdmin.Plugins.Stats.Helpers
{
    public class StatManager
    {
        private const int MAX_CACHED_HITS = 100;
        private readonly ConcurrentDictionary<long, ServerStats> _servers;
        private readonly ILogger _log;
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly IConfigurationHandler<StatsConfiguration> _configHandler;
        private static List<EFServer> serverModels;
        public static string CLIENT_STATS_KEY = "ClientStats";
        public static string CLIENT_DETECTIONS_KEY = "ClientDetections";

        public StatManager(IManager mgr, IDatabaseContextFactory contextFactory, IConfigurationHandler<StatsConfiguration> configHandler)
        {
            _servers = new ConcurrentDictionary<long, ServerStats>();
            _log = mgr.GetLogger(0);
            _contextFactory = contextFactory;
            _configHandler = configHandler;
        }

        private void SetupServerIds()
        {
            using (var ctx = _contextFactory.CreateContext(enableTracking: false))
            {
                serverModels = ctx.Set<EFServer>().ToList();
            }
        }

        public Expression<Func<EFRating, bool>> GetRankingFunc(long? serverId = null)
        {
            var fifteenDaysAgo = DateTime.UtcNow.AddDays(-15);
            return (r) => r.ServerId == serverId &&
                r.When > fifteenDaysAgo &&
                r.RatingHistory.Client.Level != EFClient.Permission.Banned &&
                r.Newest &&
                r.ActivityAmount >= _configHandler.Configuration().TopPlayersMinPlayTime;
        }

        /// <summary>
        /// gets a ranking across all servers for given client id
        /// </summary>
        /// <param name="clientId">client id of the player</param>
        /// <returns></returns>
        public async Task<int> GetClientOverallRanking(int clientId)
        {
            using (var context = _contextFactory.CreateContext(enableTracking: false))
            {
                var clientPerformance = await context.Set<EFRating>()
                    .Where(r => r.RatingHistory.ClientId == clientId)
                    .Where(r => r.ServerId == null)
                    .Where(r => r.Newest)
                    .Select(r => r.Performance)
                    .FirstOrDefaultAsync();

                if (clientPerformance != 0)
                {
                    var iqClientRanking = context.Set<EFRating>()
                        .Where(r => r.RatingHistory.ClientId != clientId)
                        .Where(r => r.Performance > clientPerformance)
                        .Where(GetRankingFunc());

                    return await iqClientRanking.CountAsync() + 1;
                }

                return 0;
            }
        }

        public async Task<List<TopStatsInfo>> GetTopStats(int start, int count, long? serverId = null)
        {
            using (var context = _contextFactory.CreateContext(enableTracking: false))
            {
                // setup the query for the clients within the given rating range
                var iqClientRatings = (from rating in context.Set<EFRating>()
                    .Where(GetRankingFunc(serverId))
                                       select new
                                       {
                                           rating.RatingHistory.ClientId,
                                           rating.RatingHistory.Client.CurrentAlias.Name,
                                           rating.RatingHistory.Client.LastConnection,
                                           rating.Performance,
                                       })
                    .OrderByDescending(c => c.Performance)
                    .Skip(start)
                    .Take(count);

                // materialized list
                var clientRatings = await iqClientRatings.ToListAsync();

                // get all the unique client ids that are in the top stats
                var clientIds = clientRatings
                    .GroupBy(r => r.ClientId)
                    .Select(r => r.First().ClientId)
                    .ToList();

                var iqRatingInfo = from rating in context.Set<EFRating>()
                                   where clientIds.Contains(rating.RatingHistory.ClientId)
                                   where rating.ServerId == serverId
                                   select new
                                   {
                                       rating.Ranking,
                                       rating.Performance,
                                       rating.RatingHistory.ClientId,
                                       rating.When
                                   };

                var ratingInfo = (await iqRatingInfo.ToListAsync())
                    .GroupBy(r => r.ClientId)
                    .Select(grp => new
                    {
                        grp.Key,
                        Ratings = grp.Select(r => new { r.Performance, r.Ranking, r.When })
                    });

                var iqStatsInfo = (from stat in context.Set<EFClientStatistics>()
                                   where clientIds.Contains(stat.ClientId)
                                   where stat.Kills > 0 || stat.Deaths > 0
                                   where serverId == null ? true : stat.ServerId == serverId
                                   group stat by stat.ClientId into s
                                   select new
                                   {
                                       ClientId = s.Key,
                                       Kills = s.Sum(c => c.Kills),
                                       Deaths = s.Sum(c => c.Deaths),
                                       KDR = s.Sum(c => (c.Kills / (double)(c.Deaths == 0 ? 1 : c.Deaths)) * c.TimePlayed) / s.Sum(c => c.TimePlayed),
                                       TotalTimePlayed = s.Sum(c => c.TimePlayed),
                                   });

                var topPlayers = await iqStatsInfo.ToListAsync();

                var clientRatingsDict = clientRatings.ToDictionary(r => r.ClientId);
                var finished = topPlayers.Select(s => new TopStatsInfo()
                {
                    ClientId = s.ClientId,
                    Id = (int?)serverId ?? 0,
                    Deaths = s.Deaths,
                    Kills = s.Kills,
                    KDR = Math.Round(s.KDR, 2),
                    LastSeen = Utilities.GetTimePassed(clientRatingsDict[s.ClientId].LastConnection, false),
                    Name = clientRatingsDict[s.ClientId].Name,
                    Performance = Math.Round(clientRatingsDict[s.ClientId].Performance, 2),
                    RatingChange = ratingInfo.First(r => r.Key == s.ClientId).Ratings.First().Ranking - ratingInfo.First(r => r.Key == s.ClientId).Ratings.Last().Ranking,
                    PerformanceHistory = ratingInfo.First(r => r.Key == s.ClientId).Ratings.Count() > 1 ?
                        ratingInfo.First(r => r.Key == s.ClientId).Ratings.OrderBy(r => r.When).Select(r => r.Performance).ToList() :
                       new List<double>() { clientRatingsDict[s.ClientId].Performance, clientRatingsDict[s.ClientId].Performance },
                    TimePlayed = Math.Round(s.TotalTimePlayed / 3600.0, 1).ToString("#,##0"),
                })
                .OrderByDescending(r => r.Performance)
                .ToList();

                // set the ranking numerically
                int i = start + 1;
                foreach (var stat in finished)
                {
                    stat.Ranking = i;
                    i++;
                }

                return finished;
            }
        }

        /// <summary>
        /// Add a server to the StatManager server pool
        /// </summary>
        /// <param name="sv"></param>
        public void AddServer(Server sv)
        {
            // insert the server if it does not exist
            try
            {
                if (serverModels == null)
                {
                    SetupServerIds();
                }

                long serverId = GetIdForServer(sv);
                EFServer server;

                using (var ctx = _contextFactory.CreateContext(enableTracking: false))
                {
                    var serverSet = ctx.Set<EFServer>();
                    // get the server from the database if it exists, otherwise create and insert a new one
                    server = serverSet.FirstOrDefault(s => s.ServerId == serverId);

                    // the server might be using legacy server id
                    if (server == null)
                    {
                        server = serverSet.FirstOrDefault(s => s.EndPoint == sv.ToString());

                        if (server != null)
                        {
                            // this provides a way to identify legacy server entries
                            server.EndPoint = sv.ToString();
                            ctx.Update(server);
                            ctx.SaveChanges();
                        }
                    }

                    // server has never been added before
                    if (server == null)
                    {
                        server = new EFServer()
                        {
                            Port = sv.Port,
                            EndPoint = sv.ToString(),
                            ServerId = serverId,
                            GameName = sv.GameName
                        };

                        server = serverSet.Add(server).Entity;
                        // this doesn't need to be async as it's during initialization
                        ctx.SaveChanges();
                    }

                    // we want to set the gamename up if it's never been set, or it changed
                    else if (!server.GameName.HasValue || server.GameName.HasValue && server.GameName.Value != sv.GameName)
                    {
                        server.GameName = sv.GameName;
                        ctx.Entry(server).Property(_prop => _prop.GameName).IsModified = true;
                        ctx.SaveChanges();
                    }
                }

                // check to see if the stats have ever been initialized
                var serverStats = InitializeServerStats(server.ServerId);

                _servers.TryAdd(serverId, new ServerStats(server, serverStats, sv)
                {
                    IsTeamBased = sv.Gametype != "dm"
                });
            }

            catch (Exception e)
            {
                _log.WriteError($"{Utilities.CurrentLocalization.LocalizationIndex["PLUGIN_STATS_ERROR_ADD"]} - {e.Message}");
                _log.WriteDebug(e.GetExceptionInfo());
            }
        }

        /// <summary>
        /// Add Player to the player stats 
        /// </summary>
        /// <param name="pl">Player to add/retrieve stats for</param>
        /// <returns>EFClientStatistic of specified player</returns>
        public async Task<EFClientStatistics> AddPlayer(EFClient pl)
        {
            try
            {
                long serverId = GetIdForServer(pl.CurrentServer);

                if (!_servers.ContainsKey(serverId))
                {
                    _log.WriteError($"[Stats::AddPlayer] Server with id {serverId} could not be found");
                    return null;
                }

                // get the client's stats from the database if it exists, otherwise create and attach a new one
                // if this fails we want to throw an exception

                EFClientStatistics clientStats;

                using (var ctx = _contextFactory.CreateContext(enableTracking: false))
                {
                    var clientStatsSet = ctx.Set<EFClientStatistics>();
                    clientStats = clientStatsSet
                        .Include(cl => cl.HitLocations)
                        .FirstOrDefault(c => c.ClientId == pl.ClientId && c.ServerId == serverId);

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
                            EloRating = 200.0,
                            HitLocations = Enum.GetValues(typeof(IW4Info.HitLocation)).OfType<IW4Info.HitLocation>().Select(hl => new EFHitLocationCount()
                            {
                                Active = true,
                                HitCount = 0,
                                Location = hl
                            }).ToList()
                        };

                        // insert if they've not been added
                        clientStats = clientStatsSet.Add(clientStats).Entity;
                        await ctx.SaveChangesAsync();
                    }

                    pl.SetAdditionalProperty(CLIENT_STATS_KEY, clientStats);

                    // migration for previous existing stats
                    if (clientStats.HitLocations.Count == 0)
                    {
                        clientStats.HitLocations = Enum.GetValues(typeof(IW4Info.HitLocation)).OfType<IW4Info.HitLocation>()
                            .Select(hl => new EFHitLocationCount()
                            {
                                Active = true,
                                HitCount = 0,
                                Location = hl
                            })
                            .ToList();

                        ctx.Update(clientStats);
                        await ctx.SaveChangesAsync();
                    }

                    // for stats before rating
                    if (clientStats.EloRating == 0.0)
                    {
                        clientStats.EloRating = clientStats.Skill;
                    }

                    if (clientStats.RollingWeightedKDR == 0)
                    {
                        clientStats.RollingWeightedKDR = clientStats.KDR;
                    }

                    // set these on connecting
                    clientStats.LastActive = DateTime.UtcNow;
                    clientStats.LastStatCalculation = DateTime.UtcNow;
                    clientStats.SessionScore = pl.Score;
                    clientStats.LastScore = pl.Score;

                    pl.SetAdditionalProperty(CLIENT_DETECTIONS_KEY, new Detection(_log, clientStats));
                    pl.CurrentServer.Logger.WriteInfo($"Added {pl} to stats");
                }

                return clientStats;
            }

            catch (Exception ex)
            {
                _log.WriteWarning("Could not add client to stats");
                _log.WriteDebug(ex.GetExceptionInfo());
            }

            return null;
        }

        /// <summary>
        /// Perform stat updates for disconnecting client
        /// </summary>
        /// <param name="pl">Disconnecting client</param>
        /// <returns></returns>
        public async Task RemovePlayer(EFClient pl)
        {
            pl.CurrentServer.Logger.WriteInfo($"Removing {pl} from stats");

            if (pl.CurrentServer == null)
            {
                pl.CurrentServer.Logger.WriteWarning($"Disconnecting client {pl} is not on a server, state is {pl.State}");
                return;
            }

            long serverId = GetIdForServer(pl.CurrentServer);
            var serverStats = _servers[serverId].ServerStatistics;

            // get individual client's stats
            var clientStats = pl.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY);
            // sync their stats before they leave
            if (clientStats != null)
            {
                clientStats = UpdateStats(clientStats);
                await SaveClientStats(clientStats);

                // increment the total play time
                serverStats.TotalPlayTime += pl.ConnectionLength;
            }

            else
            {
                pl.CurrentServer.Logger.WriteWarning($"Disconnecting client {pl} has not been added to stats, state is {pl.State}");
            }
        }

        private async Task SaveClientStats(EFClientStatistics clientStats)
        {
            using (var ctx = _contextFactory.CreateContext())
            {
                ctx.Update(clientStats);
                await ctx.SaveChangesAsync();
            }
        }

        public void AddDamageEvent(string eventLine, int attackerClientId, int victimClientId, long serverId)
        {
        }

        /// <summary>
        /// Process stats for kill event
        /// </summary>
        /// <returns></returns>
        public async Task AddScriptHit(bool isDamage, DateTime time, EFClient attacker, EFClient victim, long serverId, string map, string hitLoc, string type,
            string damage, string weapon, string killOrigin, string deathOrigin, string viewAngles, string offset, string isKillstreakKill, string Ads,
            string fraction, string visibilityPercentage, string snapAngles, string isAlive, string lastAttackTime)
        {
            Vector3 vDeathOrigin = null;
            Vector3 vKillOrigin = null;
            Vector3 vViewAngles = null;
            var snapshotAngles = new List<Vector3>();
            SemaphoreSlim waiter = null;

            try
            {
                try
                {
                    vDeathOrigin = Vector3.Parse(deathOrigin);
                    vKillOrigin = Vector3.Parse(killOrigin);
                    vViewAngles = Vector3.Parse(viewAngles).FixIW4Angles();

                    foreach (string angle in snapAngles.Split(':', StringSplitOptions.RemoveEmptyEntries))
                    {
                        snapshotAngles.Add(Vector3.Parse(angle).FixIW4Angles());
                    }
                }

                catch (FormatException)
                {
                    _log.WriteError("Could not parse vector data from hit");
                    _log.WriteDebug($"Kill - {killOrigin} Death - {deathOrigin} ViewAngle - {viewAngles} Snapshot - {string.Join(",", snapshotAngles.Select(_a => _a.ToString()))}");
                    return;
                }

                var hit = new EFClientKill()
                {
                    Active = true,
                    AttackerId = attacker.ClientId,
                    VictimId = victim.ClientId,
                    ServerId = serverId,
                    DeathOrigin = vDeathOrigin,
                    KillOrigin = vKillOrigin,
                    DeathType = ParseEnum<IW4Info.MeansOfDeath>.Get(type, typeof(IW4Info.MeansOfDeath)),
                    Damage = int.Parse(damage),
                    HitLoc = ParseEnum<IW4Info.HitLocation>.Get(hitLoc, typeof(IW4Info.HitLocation)),
                    Weapon = ParseEnum<IW4Info.WeaponName>.Get(weapon, typeof(IW4Info.WeaponName)),
                    ViewAngles = vViewAngles,
                    TimeOffset = long.Parse(offset),
                    When = time,
                    IsKillstreakKill = isKillstreakKill[0] != '0',
                    AdsPercent = float.Parse(Ads, System.Globalization.CultureInfo.InvariantCulture),
                    Fraction = double.Parse(fraction, System.Globalization.CultureInfo.InvariantCulture),
                    VisibilityPercentage = double.Parse(visibilityPercentage, System.Globalization.CultureInfo.InvariantCulture),
                    IsKill = !isDamage,
                    AnglesList = snapshotAngles,
                    IsAlive = isAlive == "1",
                    TimeSinceLastAttack = long.Parse(lastAttackTime)
                };

                if (hit.HitLoc == IW4Info.HitLocation.shield)
                {
                    // we don't care about shield hits
                    return;
                }

                var clientDetection = attacker.GetAdditionalProperty<Detection>(CLIENT_DETECTIONS_KEY);
                var clientStats = attacker.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY);

                waiter = clientStats.ProcessingHit;
                await waiter.WaitAsync(Utilities.DefaultCommandTimeout, Plugin.ServerManager.CancellationToken);

                // increment their hit count
                if (hit.DeathType == IW4Info.MeansOfDeath.MOD_PISTOL_BULLET ||
                    hit.DeathType == IW4Info.MeansOfDeath.MOD_RIFLE_BULLET ||
                    hit.DeathType == IW4Info.MeansOfDeath.MOD_HEAD_SHOT)
                {
                    clientStats.HitLocations.First(hl => hl.Location == hit.HitLoc).HitCount += 1;
                }

                if (hit.IsKillstreakKill)
                {
                    return;
                }

                if (Plugin.Config.Configuration().StoreClientKills)
                {
                    var serverWaiter = _servers[serverId].OnSaving;
                    try
                    {
                        await serverWaiter.WaitAsync();
                        var cache = _servers[serverId].HitCache;
                        cache.Add(hit);

                        if (cache.Count > MAX_CACHED_HITS)
                        {
                            await SaveHitCache(serverId);
                        }
                    }

                    catch (Exception e)
                    {
                        _log.WriteError("Could not store client kills");
                        _log.WriteDebug(e.GetExceptionInfo());
                    }

                    finally
                    {
                        if (serverWaiter.CurrentCount == 0)
                        {
                            serverWaiter.Release(1);
                        }
                    }
                }

                if (Plugin.Config.Configuration().EnableAntiCheat && !attacker.IsBot && attacker.ClientId != victim.ClientId)
                {
                    clientDetection.TrackedHits.Add(hit);

                    if (clientDetection.TrackedHits.Count >= MIN_HITS_TO_RUN_DETECTION)
                    {
                        while (clientDetection.TrackedHits.Count > 0)
                        {
                            var oldestHit = clientDetection.TrackedHits
                                .OrderBy(_hits => _hits.TimeOffset)
                                .First();

                            clientDetection.TrackedHits.Remove(oldestHit);

                            if (oldestHit.IsAlive)
                            {
                                var result = DeterminePenaltyResult(clientDetection.ProcessHit(oldestHit), attacker.CurrentServer.EndPoint);
#if !DEBUG
                                await ApplyPenalty(result, attacker);
#endif

                                if (clientDetection.Tracker.HasChanges && result.ClientPenalty != EFPenalty.PenaltyType.Any)
                                {
                                    await SaveTrackedSnapshots(clientDetection);

                                    if (result.ClientPenalty == EFPenalty.PenaltyType.Ban)
                                    {
                                        // we don't care about any additional hits now that they're banned
                                        clientDetection.TrackedHits.Clear();
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _log.WriteError("Could not save hit or AC info");
                _log.WriteDebug(ex.GetExceptionInfo());
                _log.WriteDebug($"Attacker: {attacker} Victim: {victim}, ServerId {serverId}");
            }

            finally
            {
                if (waiter?.CurrentCount == 0)
                {
                    waiter.Release();
                }
            }
        }

        private DetectionPenaltyResult DeterminePenaltyResult(IEnumerable<DetectionPenaltyResult> results, long serverId)
        {
            // allow disabling of certain detection types
            results = results.Where(_result => ShouldUseDetection(serverId, _result.Type));
            return results.FirstOrDefault(_result => _result.ClientPenalty == EFPenalty.PenaltyType.Ban) ??
                results.FirstOrDefault(_result => _result.ClientPenalty == EFPenalty.PenaltyType.Flag) ??
                new DetectionPenaltyResult()
                {
                    ClientPenalty = EFPenalty.PenaltyType.Any,
                };
        }

        public async Task SaveHitCache(long serverId)
        {
            using (var ctx = _contextFactory.CreateContext(enableTracking: false))
            {
                var server = _servers[serverId];
                ctx.AddRange(server.HitCache.ToList());
                await ctx.SaveChangesAsync();
                server.HitCache.Clear();
            }
        }

        private bool ShouldUseDetection(long serverId, DetectionType detectionType)
        {
            var detectionTypes = Plugin.Config.Configuration().ServerDetectionTypes;

            if (detectionTypes == null)
            {
                return true;
            }

            if (!detectionTypes.ContainsKey(serverId))
            {
                return true;
            }

            return detectionTypes[serverId].Contains(detectionType);
        }

        async Task ApplyPenalty(DetectionPenaltyResult penalty, EFClient attacker)
        {
            var penaltyClient = Utilities.IW4MAdminClient(attacker.CurrentServer);
            switch (penalty.ClientPenalty)
            {
                case EFPenalty.PenaltyType.Ban:
                    if (attacker.Level == EFClient.Permission.Banned)
                    {
                        break;
                    }

                    penaltyClient.AdministeredPenalties = new List<EFPenalty>()
                    {
                        new EFPenalty()
                        {
                            AutomatedOffense = penalty.Type == Detection.DetectionType.Bone ?
                                $"{penalty.Type}-{(int)penalty.Location}-{Math.Round(penalty.Value, 2)}@{penalty.HitCount}" :
                                $"{penalty.Type}-{Math.Round(penalty.Value, 2)}@{penalty.HitCount}",
                        }
                    };

                    await attacker.Ban(Utilities.CurrentLocalization.LocalizationIndex["PLUGIN_STATS_CHEAT_DETECTED"], penaltyClient, false).WaitAsync(Utilities.DefaultCommandTimeout, attacker.CurrentServer.Manager.CancellationToken);
                    break;
                case EFPenalty.PenaltyType.Flag:
                    if (attacker.Level != EFClient.Permission.User)
                    {
                        break;
                    }

                    string flagReason = penalty.Type == Cheat.Detection.DetectionType.Bone ?
                            $"{penalty.Type}-{(int)penalty.Location}-{Math.Round(penalty.Value, 2)}@{penalty.HitCount}" :
                            $"{penalty.Type}-{Math.Round(penalty.Value, 2)}@{penalty.HitCount}";

                    await attacker.Flag(flagReason, penaltyClient, new TimeSpan(168, 0, 0)).WaitAsync(Utilities.DefaultCommandTimeout, attacker.CurrentServer.Manager.CancellationToken);
                    break;
            }
        }

        async Task SaveTrackedSnapshots(Detection clientDetection)
        {
            EFACSnapshot change;

            using (var ctx = _contextFactory.CreateContext(enableTracking: false))
            {
                while ((change = clientDetection.Tracker.GetNextChange()) != default(EFACSnapshot))
                {
                    ctx.Add(change);
                }
                await ctx.SaveChangesAsync();
            }
        }

        public async Task AddStandardKill(EFClient attacker, EFClient victim)
        {
            long serverId = GetIdForServer(attacker.CurrentServer);

            var attackerStats = attacker.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY);
            var victimStats = victim.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY);

#if DEBUG
            _log.WriteDebug("Processing standard kill");
#endif
            // update the total stats
            _servers[serverId].ServerStatistics.TotalKills += 1;

            // this happens when the round has changed
            if (attackerStats.SessionScore == 0)
            {
                attackerStats.LastScore = 0;
            }

            if (victimStats.SessionScore == 0)
            {
                victimStats.LastScore = 0;
            }

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
            {
                attacker.Tell(streakMessage);
            }

            // fixme: why?
            if (double.IsNaN(victimStats.SPM) || double.IsNaN(victimStats.Skill))
            {
                _log.WriteDebug($"[StatManager::AddStandardKill] victim SPM/SKILL {victimStats.SPM} {victimStats.Skill}");
                victimStats.SPM = 0.0;
                victimStats.Skill = 0.0;
            }

            if (double.IsNaN(attackerStats.SPM) || double.IsNaN(attackerStats.Skill))
            {
                _log.WriteDebug($"[StatManager::AddStandardKill] attacker SPM/SKILL {victimStats.SPM} {victimStats.Skill}");
                attackerStats.SPM = 0.0;
                attackerStats.Skill = 0.0;
            }

            // update their performance 
            if ((DateTime.UtcNow - attackerStats.LastStatHistoryUpdate).TotalMinutes >= 2.5)
            {
                try
                {
                    // kill event is not designated as blocking, so we should be able to enter and exit
                    // we need to make this thread safe because we can potentially have kills qualify
                    // for stat history update, but one is already processing that invalidates the original
                    await attacker.Lock();
                    await UpdateStatHistory(attacker, attackerStats);
                    attackerStats.LastStatHistoryUpdate = DateTime.UtcNow;
                }

                catch (Exception e)
                {
                    _log.WriteWarning($"Could not update stat history for {attacker}");
                    _log.WriteDebug(e.GetExceptionInfo());
                }

                finally
                {
                    attacker.Unlock();
                }
            }
        }

        /// <summary>
        /// Update the invidual and average stat history for a client
        /// </summary>
        /// <param name="client">client to update</param>
        /// <param name="clientStats">stats of client that is being updated</param>
        /// <returns></returns>
        public async Task UpdateStatHistory(EFClient client, EFClientStatistics clientStats)
        {
            int currentSessionTime = (int)(DateTime.UtcNow - client.LastConnection).TotalSeconds;

            // don't update their stat history if they haven't played long
            if (currentSessionTime < 60)
            {
                return;
            }

            int currentServerTotalPlaytime = clientStats.TimePlayed + currentSessionTime;

            using (var ctx = _contextFactory.CreateContext(enableTracking: true))
            {
                // select the rating history for client
                var iqHistoryLink = from history in ctx.Set<EFClientRatingHistory>()
                                     .Include(h => h.Ratings)
                                    where history.ClientId == client.ClientId
                                    select history;

                // get the client ratings
                var clientHistory = await iqHistoryLink
                    .FirstOrDefaultAsync() ?? new EFClientRatingHistory()
                    {
                        Active = true,
                        ClientId = client.ClientId,
                        Ratings = new List<EFRating>()
                    };

                // it's the first time they've played
                if (clientHistory.RatingHistoryId == 0)
                {
                    ctx.Add(clientHistory);
                }

                #region INDIVIDUAL_SERVER_PERFORMANCE
                // get the client ranking for the current server
                int individualClientRanking = await ctx.Set<EFRating>()
                   .Where(GetRankingFunc(clientStats.ServerId))
                   // ignore themselves in the query
                   .Where(c => c.RatingHistory.ClientId != client.ClientId)
                   .Where(c => c.Performance > clientStats.Performance)
                   .CountAsync() + 1;

                // limit max history per server to 40
                if (clientHistory.Ratings.Count(r => r.ServerId == clientStats.ServerId) >= 40)
                {
                    // select the oldest one
                    var ratingToRemove = clientHistory.Ratings
                        .Where(r => r.ServerId == clientStats.ServerId)
                        .OrderBy(r => r.When)
                        .First();

                    ctx.Remove(ratingToRemove);
                }

                // set the previous newest to false
                var ratingToUnsetNewest = clientHistory.Ratings
                    .Where(r => r.ServerId == clientStats.ServerId)
                    .OrderByDescending(r => r.When)
                    .FirstOrDefault();

                if (ratingToUnsetNewest != null)
                {
                    if (ratingToUnsetNewest.Newest)
                    {
                        ctx.Update(ratingToUnsetNewest);
                        ctx.Entry(ratingToUnsetNewest).Property(r => r.Newest).IsModified = true;
                        ratingToUnsetNewest.Newest = false;
                    }
                }

                var newServerRating = new EFRating()
                {
                    Performance = clientStats.Performance,
                    Ranking = individualClientRanking,
                    Active = true,
                    Newest = true,
                    ServerId = clientStats.ServerId,
                    RatingHistory = clientHistory,
                    ActivityAmount = currentServerTotalPlaytime,
                };

                // add new rating for current server
                ctx.Add(newServerRating);

                #endregion
                #region OVERALL_RATING
                // select all performance & time played for current client
                var iqClientStats = from stats in ctx.Set<EFClientStatistics>()
                                    where stats.ClientId == client.ClientId
                                    where stats.ServerId != clientStats.ServerId
                                    select new
                                    {
                                        stats.Performance,
                                        stats.TimePlayed
                                    };

                var clientStatsList = await iqClientStats.ToListAsync();

                // add the current server's so we don't have to pull it from the database
                clientStatsList.Add(new
                {
                    clientStats.Performance,
                    TimePlayed = currentServerTotalPlaytime
                });

                // weight the overall performance based on play time
                double performanceAverage = clientStatsList.Sum(p => (p.Performance * p.TimePlayed)) / clientStatsList.Sum(p => p.TimePlayed);

                // shouldn't happen but just in case the sum of time played is 0
                if (double.IsNaN(performanceAverage))
                {
                    performanceAverage = clientStatsList.Average(p => p.Performance);
                }

                int overallClientRanking = await ctx.Set<EFRating>()
                     .Where(GetRankingFunc())
                     .Where(r => r.RatingHistory.ClientId != client.ClientId)
                     .Where(r => r.Performance > performanceAverage)
                     .CountAsync() + 1;

                // limit max average history to 40
                if (clientHistory.Ratings.Count(r => r.ServerId == null) >= 40)
                {
                    var ratingToRemove = clientHistory.Ratings
                        .Where(r => r.ServerId == null)
                        .OrderBy(r => r.When)
                        .First();

                    ctx.Remove(ratingToRemove);
                }

                // set the previous average newest to false
                ratingToUnsetNewest = clientHistory.Ratings
                    .Where(r => r.ServerId == null)
                    .OrderByDescending(r => r.When)
                    .FirstOrDefault();

                if (ratingToUnsetNewest != null)
                {
                    if (ratingToUnsetNewest.Newest)
                    {
                        ctx.Update(ratingToUnsetNewest);
                        ctx.Entry(ratingToUnsetNewest).Property(r => r.Newest).IsModified = true;
                        ratingToUnsetNewest.Newest = false;
                    }
                }

                // add new average rating
                var averageRating = new EFRating()
                {
                    Active = true,
                    Newest = true,
                    Performance = performanceAverage,
                    Ranking = overallClientRanking,
                    ServerId = null,
                    RatingHistory = clientHistory,
                    ActivityAmount = clientStatsList.Sum(s => s.TimePlayed)
                };

                ctx.Add(averageRating);
                #endregion

                await ctx.SaveChangesAsync();
            }
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

            #region DEPRECATED
            /* var validAttackerLobbyRatings = Servers[attackerStats.ServerId].PlayerStats
                 .Where(cs => cs.Value.ClientId != attackerStats.ClientId)
                 .Where(cs =>
                     Servers[attackerStats.ServerId].IsTeamBased ?
                     cs.Value.Team != attackerStats.Team :
                     cs.Value.Team != IW4Info.Team.Spectator)
                 .Where(cs => cs.Value.Team != IW4Info.Team.Spectator);

             double attackerLobbyRating = validAttackerLobbyRatings.Count() > 0 ?
                 validAttackerLobbyRatings.Average(cs => cs.Value.EloRating) :
                 attackerStats.EloRating;

             var validVictimLobbyRatings = Servers[victimStats.ServerId].PlayerStats
                 .Where(cs => cs.Value.ClientId != victimStats.ClientId)
                 .Where(cs =>
                     Servers[attackerStats.ServerId].IsTeamBased ?
                     cs.Value.Team != victimStats.Team :
                     cs.Value.Team != IW4Info.Team.Spectator)
                  .Where(cs => cs.Value.Team != IW4Info.Team.Spectator);

             double victimLobbyRating = validVictimLobbyRatings.Count() > 0 ?
                 validVictimLobbyRatings.Average(cs => cs.Value.EloRating) :
                 victimStats.EloRating;*/
            #endregion

            // calculate elo
            double attackerEloDifference = Math.Log(Math.Max(1, victimStats.EloRating)) - Math.Log(Math.Max(1, attackerStats.EloRating));
            double winPercentage = 1.0 / (1 + Math.Pow(10, attackerEloDifference / Math.E));

            // double victimEloDifference = Math.Log(Math.Max(1, attackerStats.EloRating)) - Math.Log(Math.Max(1, victimStats.EloRating));
            // double lossPercentage = 1.0 / (1 + Math.Pow(10, victimEloDifference/ Math.E));

            attackerStats.EloRating += 6.0 * (1 - winPercentage);
            victimStats.EloRating -= 6.0 * (1 - winPercentage);

            attackerStats.EloRating = Math.Max(0, Math.Round(attackerStats.EloRating, 2));
            victimStats.EloRating = Math.Max(0, Math.Round(victimStats.EloRating, 2));

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

            int scoreDifference = 0;
            // this means they've been tking or suicide and is the only time they can have a negative SPM
            if (clientStats.RoundScore < 0)
            {
                scoreDifference = clientStats.RoundScore + clientStats.LastScore;
            }

            else if (clientStats.RoundScore > 0 && clientStats.LastScore < clientStats.RoundScore)
            {
                scoreDifference = clientStats.RoundScore - clientStats.LastScore;
            }

            double killSPM = scoreDifference / timeSinceLastCalc;
            double spmMultiplier = 2.934 * Math.Pow(_servers[clientStats.ServerId].TeamCount(clientStats.Team == IW4Info.Team.Allies ? IW4Info.Team.Axis : IW4Info.Team.Allies), -0.454);
            killSPM *= Math.Max(1, spmMultiplier);

            // update this for ac tracking
            clientStats.SessionSPM = killSPM;

            // calculate how much the KDR should weigh
            // 1.637 is a Eddie-Generated number that weights the KDR nicely
            double currentKDR = clientStats.SessionDeaths == 0 ? clientStats.SessionKills : clientStats.SessionKills / clientStats.SessionDeaths;
            double alpha = Math.Sqrt(2) / Math.Min(600, Math.Max(clientStats.Kills + clientStats.Deaths, 1));
            clientStats.RollingWeightedKDR = (alpha * currentKDR) + (1.0 - alpha) * clientStats.KDR;
            double KDRWeight = Math.Round(Math.Pow(clientStats.RollingWeightedKDR, 1.637 / Math.E), 3);

            // calculate the weight of the new play time against last 10 hours of gameplay
            int totalPlayTime = (clientStats.TimePlayed == 0) ?
                (int)(DateTime.UtcNow - clientStats.LastActive).TotalSeconds :
                clientStats.TimePlayed + (int)(DateTime.UtcNow - clientStats.LastActive).TotalSeconds;

            double SPMAgainstPlayWeight = timeSinceLastCalc / Math.Min(600, (totalPlayTime / 60.0));

            // calculate the new weight against average times the weight against play time
            clientStats.SPM = (killSPM * SPMAgainstPlayWeight) + (clientStats.SPM * (1 - SPMAgainstPlayWeight));

            if (clientStats.SPM < 0)
            {
                _log.WriteWarning("[StatManager:UpdateStats] clientStats SPM < 0");
                _log.WriteDebug($"{scoreDifference}-{clientStats.RoundScore} - {clientStats.LastScore} - {clientStats.SessionScore}");
                clientStats.SPM = 0;
            }

            clientStats.SPM = Math.Round(clientStats.SPM, 3);
            clientStats.Skill = Math.Round((clientStats.SPM * KDRWeight), 3);

            // fixme: how does this happen?
            if (double.IsNaN(clientStats.SPM) || double.IsNaN(clientStats.Skill))
            {
                _log.WriteWarning("[StatManager::UpdateStats] clientStats SPM/Skill NaN");
                _log.WriteDebug($"{killSPM}-{KDRWeight}-{totalPlayTime}-{SPMAgainstPlayWeight}-{clientStats.SPM}-{clientStats.Skill}-{scoreDifference}");
                clientStats.SPM = 0;
                clientStats.Skill = 0;
            }

            clientStats.LastStatCalculation = DateTime.UtcNow;
            //clientStats.LastScore = clientStats.SessionScore;

            return clientStats;
        }

        public EFServerStatistics InitializeServerStats(long serverId)
        {
            EFServerStatistics serverStats;

            using (var ctx = _contextFactory.CreateContext(enableTracking: false))
            {
                var serverStatsSet = ctx.Set<EFServerStatistics>();
                serverStats = serverStatsSet.FirstOrDefault(s => s.ServerId == serverId);

                if (serverStats == null)
                {
                    _log.WriteDebug($"Initializing server stats for {serverId}");
                    // server stats have never been generated before
                    serverStats = new EFServerStatistics()
                    {
                        ServerId = serverId,
                        TotalKills = 0,
                        TotalPlayTime = 0,
                    };

                    serverStats = serverStatsSet.Add(serverStats).Entity;
                    ctx.SaveChanges();
                }
            }

            return serverStats;
        }

        public void ResetKillstreaks(Server sv)
        {
            foreach (var stat in sv.GetClientsAsList()
                .Select(_client => _client.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY)))
            {
                stat?.StartNewSession();
            }
        }

        public void ResetStats(EFClient client)
        {
            var stats = client.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY);
            stats.Kills = 0;
            stats.Deaths = 0;
            stats.SPM = 0;
            stats.Skill = 0;
            stats.TimePlayed = 0;
            stats.EloRating = 200;
        }

        public async Task AddMessageAsync(int clientId, long serverId, string message)
        {
            // the web users can have no account
            if (clientId < 1)
            {
                return;
            }

            using (var ctx = _contextFactory.CreateContext(enableTracking: false))
            {
                ctx.Set<EFClientMessage>().Add(new EFClientMessage()
                {
                    ClientId = clientId,
                    Message = message,
                    ServerId = serverId,
                    TimeSent = DateTime.UtcNow
                });

                await ctx.SaveChangesAsync();
            }
        }

        public async Task Sync(Server sv)
        {
            long serverId = GetIdForServer(sv);

            var waiter = _servers[serverId].OnSaving;
            try
            {
                await waiter.WaitAsync();

                using (var ctx = _contextFactory.CreateContext())
                {
                    var serverStatsSet = ctx.Set<EFServerStatistics>();
                    serverStatsSet.Update(_servers[serverId].ServerStatistics);
                    await ctx.SaveChangesAsync();
                }

                foreach (var stats in sv.GetClientsAsList()
                    .Select(_client => _client.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY))
                    .Where(_stats => _stats != null))
                {
                    await SaveClientStats(stats);
                }

                await SaveHitCache(serverId);
            }

            catch (Exception e)
            {
                _log.WriteError("There was a probably syncing server stats");
                _log.WriteDebug(e.GetExceptionInfo());
            }

            finally
            {
                if (waiter.CurrentCount == 0)
                {
                    waiter.Release(1);
                }
            }
        }

        public void SetTeamBased(long serverId, bool isTeamBased)
        {
            _servers[serverId].IsTeamBased = isTeamBased;
        }

        public static long GetIdForServer(Server server)
        {
            if ($"{server.IP}:{server.Port.ToString()}" == "66.150.121.184:28965")
            {
                return 886229536;
            }

            long id = HashCode.Combine(server.IP, server.Port);
            id = id < 0 ? Math.Abs(id) : id;
            long? serverId;

            serverId = serverModels.FirstOrDefault(_server => _server.ServerId == server.EndPoint ||
                _server.EndPoint == server.ToString() ||
                _server.ServerId == id)?.ServerId;

            if (!serverId.HasValue)
            {
                return id;
            }

            return serverId.Value;
        }
    }
}
