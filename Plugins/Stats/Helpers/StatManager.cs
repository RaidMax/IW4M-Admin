using IW4MAdmin.Plugins.Stats.Cheat;
using IW4MAdmin.Plugins.Stats.Config;
using IW4MAdmin.Plugins.Stats.Web.Dtos;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Context;
using Data.Models;
using Data.Models.Client;
using Data.Models.Client.Stats;
using Data.Models.Server;
using Humanizer.Localisation;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;
using Stats.Client.Abstractions;
using Stats.Config;
using Stats.Helpers;
using static IW4MAdmin.Plugins.Stats.Cheat.Detection;
using EFClient = SharedLibraryCore.Database.Models.EFClient;
using ILogger = Microsoft.Extensions.Logging.ILogger;

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
        public static string ESTIMATED_SCORE = "EstimatedScore";
        private readonly SemaphoreSlim _addPlayerWaiter = new SemaphoreSlim(1, 1);
        private readonly IServerDistributionCalculator _serverDistributionCalculator;

        public StatManager(ILogger<StatManager> logger, IManager mgr, IDatabaseContextFactory contextFactory,
            IConfigurationHandler<StatsConfiguration> configHandler,
            IServerDistributionCalculator serverDistributionCalculator)
        {
            _servers = new ConcurrentDictionary<long, ServerStats>();
            _log = logger;
            _contextFactory = contextFactory;
            _configHandler = configHandler;
            _serverDistributionCalculator = serverDistributionCalculator;
        }

        ~StatManager()
        {
            _addPlayerWaiter.Dispose();
        }

        private void SetupServerIds()
        {
            using var ctx = _contextFactory.CreateContext(enableTracking: false);
            serverModels = ctx.Set<EFServer>().ToList();
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
        public async Task<int> GetClientOverallRanking(int clientId, long? serverId = null)
        {
            await using var context = _contextFactory.CreateContext(enableTracking: false);
            
            if (_configHandler.Configuration().EnableAdvancedMetrics)
            {
                var clientRanking = await context.Set<EFClientRankingHistory>()
                    .Where(r => r.ClientId == clientId)
                    .Where(r => r.ServerId == serverId)
                    .Where(r => r.Newest)
                    .FirstOrDefaultAsync();
                return clientRanking?.Ranking + 1 ?? 0;
            }

            var clientPerformance = await context.Set<EFRating>()
                .Where(r => r.RatingHistory.ClientId == clientId)
                .Where(r => r.ServerId == serverId)
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

        public Expression<Func<EFClientRankingHistory, bool>> GetNewRankingFunc(int? clientId = null, long? serverId = null)
        {
            return (ranking) => ranking.ServerId == serverId
                                && ranking.Client.Level != Data.Models.Client.EFClient.Permission.Banned
                                && ranking.CreatedDateTime >= Extensions.FifteenDaysAgo()
                                && ranking.ZScore != null
                                && ranking.PerformanceMetric != null
                                && ranking.Newest
                                && ranking.Client.TotalConnectionTime >=
                                _configHandler.Configuration().TopPlayersMinPlayTime;
        }

        public async Task<int> GetTotalRankedPlayers(long serverId)
        {
            await using var context = _contextFactory.CreateContext(enableTracking: false);

            return await context.Set<EFClientRankingHistory>()
                .Where(GetNewRankingFunc(serverId: serverId))
                .CountAsync();
        }

        public async Task<List<TopStatsInfo>> GetNewTopStats(int start, int count, long? serverId = null)
        {
            await using var context = _contextFactory.CreateContext(false);

            var clientIdsList = await context.Set<EFClientRankingHistory>()
                .Where(GetNewRankingFunc(serverId: serverId))
                .OrderByDescending(ranking => ranking.PerformanceMetric)
                .Select(ranking => ranking.ClientId)
                .Skip(start)
                .Take(count)
                .ToListAsync();

           var rankings = await context.Set<EFClientRankingHistory>()
                .Where(ranking => clientIdsList.Contains(ranking.ClientId))
                .Where(ranking => ranking.ServerId == serverId)
                .Select(ranking => new
                {
                    ranking.ClientId,
                    ranking.Client.CurrentAlias.Name,
                    ranking.Client.LastConnection,
                    ranking.PerformanceMetric,
                    ranking.ZScore,
                    ranking.Ranking,
                    ranking.CreatedDateTime
                })
                .ToListAsync();

            var rankingsDict = rankings.GroupBy(rank => rank.ClientId)
                .ToDictionary(rank => rank.Key, rank => rank.OrderBy(r => r.CreatedDateTime).ToList());
            
            var statsInfo = await context.Set<EFClientStatistics>()
                .Where(stat => clientIdsList.Contains(stat.ClientId))
                .Where(stat => stat.TimePlayed > 0)
                .Where(stat => stat.Kills > 0 || stat.Deaths > 0)
                .Where(stat => serverId == null || stat.ServerId == serverId)
                .GroupBy(stat => stat.ClientId)
                .Select(s => new
                {
                    ClientId = s.Key,
                    Kills = s.Sum(c => c.Kills),
                    Deaths = s.Sum(c => c.Deaths),
                    KDR = s.Sum(c => (c.Kills / (double) (c.Deaths == 0 ? 1 : c.Deaths)) * c.TimePlayed) /
                          s.Sum(c => c.TimePlayed),
                    TotalTimePlayed = s.Sum(c => c.TimePlayed),
                    UpdatedAt = s.Max(c => c.UpdatedAt)
                })
                .ToListAsync();

            var finished = statsInfo
                .OrderByDescending(stat => rankingsDict[stat.ClientId].Last().PerformanceMetric)
                .Select((s, index) => new TopStatsInfo()
            {
                ClientId = s.ClientId,
                Id = (int?) serverId ?? 0,
                Deaths = s.Deaths,
                Kills = s.Kills,
                KDR = Math.Round(s.KDR, 2),
                LastSeen = (DateTime.UtcNow - (s.UpdatedAt ?? rankingsDict[s.ClientId].Last().LastConnection))
                    .HumanizeForCurrentCulture(1, TimeUnit.Week, TimeUnit.Second, ",", false),
                LastSeenValue = DateTime.UtcNow - (s.UpdatedAt ?? rankingsDict[s.ClientId].Last().LastConnection),
                Name = rankingsDict[s.ClientId].First().Name,
                Performance = Math.Round(rankingsDict[s.ClientId].Last().PerformanceMetric ?? 0, 2),
                RatingChange = (rankingsDict[s.ClientId].First().Ranking -
                                rankingsDict[s.ClientId].Last().Ranking) ?? 0,
                PerformanceHistory = rankingsDict[s.ClientId].Select(ranking => ranking.PerformanceMetric ?? 0).ToList(),
                TimePlayed = Math.Round(s.TotalTimePlayed / 3600.0, 1).ToString("#,##0"),
                TimePlayedValue = TimeSpan.FromSeconds(s.TotalTimePlayed),
                Ranking = index + start + 1,
                ZScore = rankingsDict[s.ClientId].Last().ZScore,
                ServerId = serverId
            })
            .OrderBy(r => r.Ranking)
            .ToList();

            return finished;
        }

        public async Task<List<TopStatsInfo>> GetTopStats(int start, int count, long? serverId = null)
        {
            if (_configHandler.Configuration().EnableAdvancedMetrics)
            {
                return await GetNewTopStats(start, count, serverId);
            }
            
            await using var context = _contextFactory.CreateContext(enableTracking: false);
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
            var clientRatings = (await iqClientRatings.ToListAsync())
                .GroupBy(rating => rating.ClientId) // prevent duplicate keys
                .Select(group => group.FirstOrDefault());

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
                    Ratings = grp.Select(r => new {r.Performance, r.Ranking, r.When})
                });

            var iqStatsInfo = (from stat in context.Set<EFClientStatistics>()
                where clientIds.Contains(stat.ClientId)
                where stat.Kills > 0 || stat.Deaths > 0
                where serverId == null || stat.ServerId == serverId
                group stat by stat.ClientId
                into s
                select new
                {
                    ClientId = s.Key,
                    Kills = s.Sum(c => c.Kills),
                    Deaths = s.Sum(c => c.Deaths),
                    KDR = s.Sum(c => (c.Kills / (double) (c.Deaths == 0 ? 1 : c.Deaths)) * c.TimePlayed) /
                          s.Sum(c => c.TimePlayed),
                    TotalTimePlayed = s.Sum(c => c.TimePlayed),
                });

            var topPlayers = await iqStatsInfo.ToListAsync();

            var clientRatingsDict = clientRatings.ToDictionary(r => r.ClientId);
            var finished = topPlayers.Select(s => new TopStatsInfo()
                {
                    ClientId = s.ClientId,
                    Id = (int?) serverId ?? 0,
                    Deaths = s.Deaths,
                    Kills = s.Kills,
                    KDR = Math.Round(s.KDR, 2),
                    LastSeen = (DateTime.UtcNow - clientRatingsDict[s.ClientId].LastConnection)
                        .HumanizeForCurrentCulture(),
                    LastSeenValue = DateTime.UtcNow - clientRatingsDict[s.ClientId].LastConnection,
                    Name = clientRatingsDict[s.ClientId].Name,
                    Performance = Math.Round(clientRatingsDict[s.ClientId].Performance, 2),
                    RatingChange = ratingInfo.First(r => r.Key == s.ClientId).Ratings.First().Ranking -
                                   ratingInfo.First(r => r.Key == s.ClientId).Ratings.Last().Ranking,
                    PerformanceHistory = ratingInfo.First(r => r.Key == s.ClientId).Ratings.Count() > 1
                        ? ratingInfo.First(r => r.Key == s.ClientId).Ratings.OrderBy(r => r.When)
                            .Select(r => r.Performance).ToList()
                        : new List<double>()
                            {clientRatingsDict[s.ClientId].Performance, clientRatingsDict[s.ClientId].Performance},
                    TimePlayed = Math.Round(s.TotalTimePlayed / 3600.0, 1).ToString("#,##0"),
                    TimePlayedValue = TimeSpan.FromSeconds(s.TotalTimePlayed)
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

                using var ctx = _contextFactory.CreateContext(enableTracking: false);
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
                        GameName = (Reference.Game?) sv.GameName,
                        HostName = sv.Hostname
                    };

                    server = serverSet.Add(server).Entity;
                    // this doesn't need to be async as it's during initialization
                    ctx.SaveChanges();
                }

                // we want to set the gamename up if it's never been set, or it changed
                else if (!server.GameName.HasValue || server.GameName.Value != (Reference.Game) sv.GameName)
                {
                    server.GameName = (Reference.Game) sv.GameName;
                    ctx.Entry(server).Property(_prop => _prop.GameName).IsModified = true;
                    ctx.SaveChanges();
                }

                if (server.HostName == null || server.HostName != sv.Hostname)
                {
                    server.HostName = sv.Hostname;
                    ctx.Entry(server).Property(_prop => _prop.HostName).IsModified = true;
                    ctx.SaveChanges();
                }

                ctx.Entry(server).Property(_prop => _prop.IsPasswordProtected).IsModified = true;
                server.IsPasswordProtected = !string.IsNullOrEmpty(sv.GamePassword);
                ctx.SaveChanges();

                // check to see if the stats have ever been initialized
                var serverStats = InitializeServerStats(server.ServerId);

                _servers.TryAdd(serverId, new ServerStats(server, serverStats, sv)
                {
                    IsTeamBased = sv.Gametype != "dm"
                });
            }

            catch (Exception e)
            {
                _log.LogError(e, "{message}",
                    Utilities.CurrentLocalization.LocalizationIndex["PLUGIN_STATS_ERROR_ADD"]);
            }
        }

        /// <summary>
        /// Add Player to the player stats 
        /// </summary>
        /// <param name="pl">Player to add/retrieve stats for</param>
        /// <returns>EFClientStatistic of specified player</returns>
        public async Task<EFClientStatistics> AddPlayer(EFClient pl)
        {
            var existingStats = pl.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY);

            if (existingStats != null)
            {
                return existingStats;
            }

            try
            {
                await _addPlayerWaiter.WaitAsync();
                long serverId = GetIdForServer(pl.CurrentServer);

                if (!_servers.ContainsKey(serverId))
                {
                    _log.LogError("[Stats::AddPlayer] Server with id {serverId} could not be found", serverId);
                    return null;
                }

                if (pl.ClientId <= 0)
                {
                    _log.LogWarning("Stats for {Client} are not yet initialized", pl.ToString());
                    return null;
                }

                // get the client's stats from the database if it exists, otherwise create and attach a new one
                // if this fails we want to throw an exception

                EFClientStatistics clientStats;

                await using var ctx = _contextFactory.CreateContext(enableTracking: false);
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
                        HitLocations = Enum.GetValues(typeof(IW4Info.HitLocation)).OfType<IW4Info.HitLocation>()
                            .Select(hl => new EFHitLocationCount()
                            {
                                Active = true,
                                HitCount = 0,
                                Location = (int) hl
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
                    clientStats.HitLocations = Enum.GetValues(typeof(IW4Info.HitLocation))
                        .OfType<IW4Info.HitLocation>()
                        .Select(hl => new EFHitLocationCount()
                        {
                            Active = true,
                            HitCount = 0,
                            Location = (int) hl
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
                _log.LogDebug("Added {client} to stats", pl.ToString());

                return clientStats;
            }

            catch (DbUpdateException updateException) when (
                updateException.InnerException is PostgresException {SqlState: "23503"}
                || updateException.InnerException is SqliteException {SqliteErrorCode: 787}
                || updateException.InnerException is MySqlException {SqlState: "23503"})
            {
                _log.LogWarning("Trying to add {Client} to stats before they have been added to the database",
                    pl.ToString());
            }

            catch (Exception ex)
            {
                _log.LogError(ex, "Could not add client to stats {@client}", pl.ToString());
            }

            finally
            {
                if (_addPlayerWaiter.CurrentCount == 0)
                {
                    _addPlayerWaiter.Release(1);
                }
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
            _log.LogDebug("Removing {client} from stats", pl.ToString());

            if (pl.CurrentServer == null)
            {
                _log.LogWarning("Disconnecting client {client} is not on a server", pl.ToString());
                return;
            }

            var serverId = GetIdForServer(pl.CurrentServer);
            var serverStats = _servers[serverId].ServerStatistics;

            // get individual client's stats
            var clientStats = pl.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY);
            // sync their stats before they leave
            if (clientStats != null)
            {
                clientStats = UpdateStats(clientStats, pl);
                await SaveClientStats(clientStats);
                if (_configHandler.Configuration().EnableAdvancedMetrics)
                {
                    await UpdateHistoricalRanking(pl.ClientId, clientStats, serverId);
                }

                // increment the total play time
                serverStats.TotalPlayTime += pl.ConnectionLength;
                pl.SetAdditionalProperty(CLIENT_STATS_KEY, null);
            }

            else
            {
                _log.LogWarning("Disconnecting client {client} has not been added to stats", pl.ToString());
            }
        }

        private async Task SaveClientStats(EFClientStatistics clientStats)
        {
            await using var ctx = _contextFactory.CreateContext();
            ctx.Update(clientStats);
            await ctx.SaveChangesAsync();
        }

        public void AddDamageEvent(string eventLine, int attackerClientId, int victimClientId, long serverId)
        {
        }

        /// <summary>
        /// Process stats for kill event
        /// </summary>
        /// <returns></returns>
        public async Task AddScriptHit(bool isDamage, DateTime time, EFClient attacker, EFClient victim, long serverId,
            string map, string hitLoc, string type,
            string damage, string weapon, string killOrigin, string deathOrigin, string viewAngles, string offset,
            string isKillstreakKill, string Ads,
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

                catch (FormatException ex)
                {
                    _log.LogWarning(ex, "Could not parse vector data from hit");
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
                    DeathType = (int) ParseEnum<IW4Info.MeansOfDeath>.Get(type, typeof(IW4Info.MeansOfDeath)),
                    Damage = int.Parse(damage),
                    HitLoc = (int) ParseEnum<IW4Info.HitLocation>.Get(hitLoc, typeof(IW4Info.HitLocation)),
                    WeaponReference = weapon,
                    ViewAngles = vViewAngles,
                    TimeOffset = long.Parse(offset),
                    When = time,
                    IsKillstreakKill = isKillstreakKill[0] != '0',
                    AdsPercent = float.Parse(Ads, System.Globalization.CultureInfo.InvariantCulture),
                    Fraction = double.Parse(fraction, System.Globalization.CultureInfo.InvariantCulture),
                    VisibilityPercentage = double.Parse(visibilityPercentage,
                        System.Globalization.CultureInfo.InvariantCulture),
                    IsKill = !isDamage,
                    AnglesList = snapshotAngles,
                    IsAlive = isAlive == "1",
                    TimeSinceLastAttack = long.Parse(lastAttackTime),
                    GameName = (int) attacker.CurrentServer.GameName
                };
                
                hit.SetAdditionalProperty("HitLocationReference", hitLoc);

                if (hit.HitLoc == (int) IW4Info.HitLocation.shield)
                {
                    // we don't care about shield hits
                    return;
                }

                var clientDetection = attacker.GetAdditionalProperty<Detection>(CLIENT_DETECTIONS_KEY);
                var clientStats = attacker.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY);

                if (clientDetection == null || clientStats?.ClientId == null)
                {
                    _log.LogWarning("Client stats state for {Client} is not yet initialized", attacker.ToString());
                    return;
                }

                waiter = clientStats.ProcessingHit;
                await waiter.WaitAsync(Utilities.DefaultCommandTimeout, Plugin.ServerManager.CancellationToken);

                // increment their hit count
                if (hit.DeathType == (int) IW4Info.MeansOfDeath.MOD_PISTOL_BULLET ||
                    hit.DeathType == (int) IW4Info.MeansOfDeath.MOD_RIFLE_BULLET ||
                    hit.DeathType == (int) IW4Info.MeansOfDeath.MOD_HEAD_SHOT)
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
                        _log.LogError(e, "Could not store client kills");
                    }

                    finally
                    {
                        if (serverWaiter.CurrentCount == 0)
                        {
                            serverWaiter.Release(1);
                        }
                    }
                }

                if (Plugin.Config.Configuration().AnticheatConfiguration.Enable && !attacker.IsBot &&
                    attacker.ClientId != victim.ClientId)
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
                                var result = DeterminePenaltyResult(clientDetection.ProcessHit(oldestHit), attacker);

                                if (!Utilities.IsDevelopment)
                                {
                                    await ApplyPenalty(result, attacker);
                                }

                                if (clientDetection.Tracker.HasChanges &&
                                    result.ClientPenalty != EFPenalty.PenaltyType.Any)
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

            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Could not save hit or anti-cheat info {@attacker} {@victim} {server}", attacker,
                    victim, serverId);
            }

            finally
            {
                if (waiter?.CurrentCount == 0)
                {
                    waiter.Release();
                }
            }
        }

        private DetectionPenaltyResult DeterminePenaltyResult(IEnumerable<DetectionPenaltyResult> results,
            EFClient client)
        {
            // allow disabling of certain detection types
            results = results.Where(_result => ShouldUseDetection(client.CurrentServer, _result.Type, client.ClientId));
            return results.FirstOrDefault(_result => _result.ClientPenalty == EFPenalty.PenaltyType.Ban) ??
                   results.FirstOrDefault(_result => _result.ClientPenalty == EFPenalty.PenaltyType.Flag) ??
                   new DetectionPenaltyResult()
                   {
                       ClientPenalty = EFPenalty.PenaltyType.Any,
                   };
        }

        public async Task SaveHitCache(long serverId)
        {
            await using var ctx = _contextFactory.CreateContext(enableTracking: false);
            var server = _servers[serverId];
            ctx.AddRange(server.HitCache.ToList());
            await ctx.SaveChangesAsync();
            server.HitCache.Clear();
        }

        private bool ShouldUseDetection(Server server, DetectionType detectionType, long clientId)
        {
            var detectionTypes = Plugin.Config.Configuration().AnticheatConfiguration.ServerDetectionTypes;
            var ignoredClients = Plugin.Config.Configuration().AnticheatConfiguration.IgnoredClientIds;

            if (ignoredClients.Contains(clientId))
            {
                return false;
            }


            try
            {
                if (!detectionTypes[server.EndPoint].Contains(detectionType))
                {
                    return false;
                }
            }

            catch (KeyNotFoundException)
            {
            }

            return true;
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
                            AutomatedOffense = penalty.Type == Detection.DetectionType.Bone
                                ? $"{penalty.Type}-{(int) penalty.Location}-{Math.Round(penalty.Value, 2)}@{penalty.HitCount}"
                                : $"{penalty.Type}-{Math.Round(penalty.Value, 2)}@{penalty.HitCount}",
                        }
                    };

                    await attacker
                        .Ban(Utilities.CurrentLocalization.LocalizationIndex["PLUGIN_STATS_CHEAT_DETECTED"],
                            penaltyClient, false).WaitAsync(Utilities.DefaultCommandTimeout,
                            attacker.CurrentServer.Manager.CancellationToken);
                    break;
                case EFPenalty.PenaltyType.Flag:
                    if (attacker.Level != EFClient.Permission.User)
                    {
                        break;
                    }

                    string flagReason = penalty.Type == Cheat.Detection.DetectionType.Bone
                        ? $"{penalty.Type}-{(int) penalty.Location}-{Math.Round(penalty.Value, 2)}@{penalty.HitCount}"
                        : $"{penalty.Type}-{Math.Round(penalty.Value, 2)}@{penalty.HitCount}";

                    penaltyClient.AdministeredPenalties = new List<EFPenalty>()
                    {
                        new EFPenalty()
                        {
                            AutomatedOffense = flagReason
                        }
                    };

                    await attacker.Flag(flagReason, penaltyClient, new TimeSpan(168, 0, 0))
                        .WaitAsync(Utilities.DefaultCommandTimeout, attacker.CurrentServer.Manager.CancellationToken);
                    break;
            }
        }

        async Task SaveTrackedSnapshots(Detection clientDetection)
        {
            EFACSnapshot change;

            await using var ctx = _contextFactory.CreateContext();
            while ((change = clientDetection.Tracker.GetNextChange()) != default(EFACSnapshot))
            {
                ctx.Add(change);
            }

            await ctx.SaveChangesAsync();
        }

        public async Task AddStandardKill(EFClient attacker, EFClient victim)
        {
            var serverId = GetIdForServer(attacker.CurrentServer);

            var attackerStats = attacker.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY);
            var victimStats = victim.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY);

            // update the total stats
            _servers[serverId].ServerStatistics.TotalKills += 1;
            
            if (attackerStats == null)
            {
                _log.LogWarning("Stats for {Client} are not yet initialized", attacker.ToString());
                return;
            }
            
            if (victimStats == null)
            {
                _log.LogWarning("Stats for {Client} are not yet initialized", victim.ToString());
                return;
            }
            
            // this happens when the round has changed
            if (attackerStats.SessionScore == 0)
            {
                attackerStats.LastScore = 0;
            }

            if (victimStats.SessionScore == 0)
            {
                victimStats.LastScore = 0;
            }

            var estimatedAttackerScore = attacker.CurrentServer.GameName != Server.Game.CSGO
                ? attacker.Score 
                : attackerStats.SessionKills * 50;
            var estimatedVictimScore = attacker.CurrentServer.GameName != Server.Game.CSGO  
                ? victim.Score 
                : victimStats.SessionKills * 50;

            attackerStats.SessionScore = estimatedAttackerScore;
            victimStats.SessionScore = estimatedVictimScore;

            attacker.SetAdditionalProperty(ESTIMATED_SCORE, estimatedAttackerScore);
            victim.SetAdditionalProperty(ESTIMATED_SCORE, estimatedVictimScore);

            // calculate for the clients
            CalculateKill(attackerStats, victimStats, attacker, victim);
            // this should fix the negative SPM
            // updates their last score after being calculated
            attackerStats.LastScore = estimatedAttackerScore;
            victimStats.LastScore = estimatedVictimScore;

            // show encouragement/discouragement
            var streakMessage = (attackerStats.ClientId != victimStats.ClientId)
                ? StreakMessage.MessageOnStreak(attackerStats.KillStreak, attackerStats.DeathStreak)
                : StreakMessage.MessageOnStreak(-1, -1);

            if (streakMessage != string.Empty)
            {
                attacker.Tell(streakMessage);
            }

            // fixme: why?
            if (double.IsNaN(victimStats.SPM) || double.IsNaN(victimStats.Skill))
            {
                _log.LogWarning("victim SPM/SKILL {@victimStats}", victimStats);
                victimStats.SPM = 0.0;
                victimStats.Skill = 0.0;
            }

            if (double.IsNaN(attackerStats.SPM) || double.IsNaN(attackerStats.Skill))
            {
                _log.LogWarning("attacker SPM/SKILL {@attackerStats}", attackerStats);
                attackerStats.SPM = 0.0;
                attackerStats.Skill = 0.0;
            }

            // update their performance 
            if ((DateTime.UtcNow - attackerStats.LastStatHistoryUpdate).TotalMinutes >=
                (Utilities.IsDevelopment ? 0.5 : _configHandler.Configuration().EnableAdvancedMetrics ? 5.0 : 2.5))
            {
                try
                {
                    // kill event is not designated as blocking, so we should be able to enter and exit
                    // we need to make this thread safe because we can potentially have kills qualify
                    // for stat history update, but one is already processing that invalidates the original
                    await attackerStats.ProcessingHit.WaitAsync(Utilities.DefaultCommandTimeout,
                        Plugin.ServerManager.CancellationToken);
                    if (_configHandler.Configuration().EnableAdvancedMetrics)
                    {
                        await UpdateHistoricalRanking(attacker.ClientId, attackerStats, serverId);
                    }

                    else
                    {
                        await UpdateStatHistory(attacker, attackerStats);
                    }

                    attackerStats.LastStatHistoryUpdate = DateTime.UtcNow;
                }

                catch (Exception e)
                {
                    _log.LogWarning(e, "Could not update stat history for {attacker}", attacker.ToString());
                }

                finally
                {
                    if (attackerStats.ProcessingHit.CurrentCount == 0)
                    {
                        attackerStats.ProcessingHit.Release(1);
                    }
                }
            }
        }

        /// <summary>
        /// Update the individual and average stat history for a client
        /// </summary>
        /// <param name="client">client to update</param>
        /// <param name="clientStats">stats of client that is being updated</param>
        /// <returns></returns>
        public async Task UpdateStatHistory(EFClient client, EFClientStatistics clientStats)
        {
            int currentSessionTime = (int) (DateTime.UtcNow - client.LastConnection).TotalSeconds;

            // don't update their stat history if they haven't played long
            if (currentSessionTime < 60)
            {
                return;
            }

            int currentServerTotalPlaytime = clientStats.TimePlayed + currentSessionTime;

            await using var ctx = _contextFactory.CreateContext(enableTracking: true);
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
            double performanceAverage = clientStatsList.Sum(p => (p.Performance * p.TimePlayed)) /
                                        clientStatsList.Sum(p => p.TimePlayed);

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

        public async Task UpdateHistoricalRanking(int clientId, EFClientStatistics clientStats, long serverId)
        {
            await using var context = _contextFactory.CreateContext();
            var minPlayTime = _configHandler.Configuration().TopPlayersMinPlayTime;
            
            var performances = await context.Set<EFClientStatistics>()
                .AsNoTracking()
                .Where(stat => stat.ClientId == clientId)
                .Where(stat => stat.ServerId != serverId) // ignore the one we're currently tracking
                .Where(stats => stats.UpdatedAt >= Extensions.FifteenDaysAgo())
                .Where(stats => stats.TimePlayed >= minPlayTime)
                .ToListAsync();
            
            if (clientStats.TimePlayed >= minPlayTime)
            {
                clientStats.ZScore = await _serverDistributionCalculator.GetZScoreForServer(serverId,
                    clientStats.Performance);

                var serverRanking = await context.Set<EFClientStatistics>()
                    .Where(stats => stats.ClientId != clientStats.ClientId)
                    .Where(AdvancedClientStatsResourceQueryHelper.GetRankingFunc(
                        _configHandler.Configuration().TopPlayersMinPlayTime, clientStats.ZScore, serverId))
                    .CountAsync();

                var serverRankingSnapshot = new EFClientRankingHistory
                {
                    ClientId = clientId,
                    ServerId = serverId,
                    ZScore = clientStats.ZScore,
                    Ranking = serverRanking,
                    PerformanceMetric = clientStats.Performance,
                    Newest = true
                };

                context.Add(serverRankingSnapshot);
                await PruneOldRankings(context, clientId, serverId);
                await context.SaveChangesAsync();

                performances.Add(clientStats);
            }

            if (performances.Any(performance => performance.TimePlayed >= minPlayTime))
            {
                var aggregateZScore = performances.WeightValueByPlaytime(nameof(EFClientStatistics.ZScore), minPlayTime);
                
                int? aggregateRanking = await context.Set<EFClientStatistics>()
                    .Where(stat => stat.ClientId != clientId)
                    .Where(AdvancedClientStatsResourceQueryHelper.GetRankingFunc(minPlayTime))
                    .GroupBy(stat => stat.ClientId)
                    .Where(group =>
                        group.Sum(stat => stat.ZScore * stat.TimePlayed) / group.Sum(stat => stat.TimePlayed) >
                        aggregateZScore)
                    .Select(c => c.Key)
                    .CountAsync();

                var newPerformanceMetric = await _serverDistributionCalculator.GetRatingForZScore(aggregateZScore);

                if (newPerformanceMetric == null)
                {
                    _log.LogWarning("Could not determine performance metric for {Client} {AggregateZScore}",
                        clientStats.Client?.ToString(), aggregateZScore);
                    return;
                }
                
                var aggregateRankingSnapshot = new EFClientRankingHistory
                {
                    ClientId = clientId,
                    ZScore = aggregateZScore,
                    Ranking = aggregateRanking,
                    PerformanceMetric = newPerformanceMetric,
                    Newest = true,
                };

                context.Add(aggregateRankingSnapshot);

                await PruneOldRankings(context, clientId);
                await context.SaveChangesAsync();
            }
        }

        private async Task PruneOldRankings(DatabaseContext context, int clientId, long? serverId = null)
        {
            var totalRankingEntries = await context.Set<EFClientRankingHistory>()
                .Where(r => r.ClientId == clientId)
                .Where(r => r.ServerId == serverId)
                .CountAsync();
            
            var mostRecent = await context.Set<EFClientRankingHistory>()
                .Where(r => r.ClientId == clientId)
                .Where(r => r.ServerId == serverId)
                .FirstOrDefaultAsync(r => r.Newest);

            if (mostRecent != null)
            {
                mostRecent.Newest = false;
                context.Update(mostRecent);
            }

            if (totalRankingEntries > EFClientRankingHistory.MaxRankingCount)
            {
                var lastRating = await context.Set<EFClientRankingHistory>()
                    .Where(r => r.ClientId == clientId)
                    .Where(r => r.ServerId == serverId)
                    .OrderBy(r => r.CreatedDateTime)
                    .FirstOrDefaultAsync();
                context.Remove(lastRating);
            }
        }

        /// <summary>
        /// Performs the incrementation of kills and deaths for client statistics
        /// </summary>
        /// <param name="attackerStats">Stats of the attacker</param>
        /// <param name="victimStats">Stats of the victim</param>
        public void CalculateKill(EFClientStatistics attackerStats, EFClientStatistics victimStats, 
            EFClient attacker, EFClient victim)
        {
            bool suicide = attackerStats.ClientId == victimStats.ClientId;

            // only update their kills if they didn't kill themselves
            if (!suicide)
            {
                attackerStats.Kills += 1;
                attackerStats.MatchData.Kills += 1;
                attackerStats.SessionKills += 1;
                attackerStats.KillStreak += 1;
                attackerStats.DeathStreak = 0;
            }

            victimStats.Deaths += 1;
            victimStats.MatchData.Deaths += 1;
            victimStats.SessionDeaths += 1;
            victimStats.DeathStreak += 1;
            victimStats.KillStreak = 0;

            // process the attacker's stats after the kills
            attackerStats = UpdateStats(attackerStats, attacker);

            // calculate elo
            var attackerEloDifference = Math.Log(Math.Max(1, victimStats.EloRating)) -
                                           Math.Log(Math.Max(1, attackerStats.EloRating));
            var winPercentage = 1.0 / (1 + Math.Pow(10, attackerEloDifference / Math.E));

            attackerStats.EloRating += 6.0 * (1 - winPercentage);
            victimStats.EloRating -= 6.0 * (1 - winPercentage);

            attackerStats.EloRating = Math.Max(0, Math.Round(attackerStats.EloRating, 2));
            victimStats.EloRating = Math.Max(0, Math.Round(victimStats.EloRating, 2));

            // update after calculation
            attackerStats.TimePlayed += (int) (DateTime.UtcNow - attackerStats.LastActive).TotalSeconds;
            victimStats.TimePlayed += (int) (DateTime.UtcNow - victimStats.LastActive).TotalSeconds;
            attackerStats.LastActive = DateTime.UtcNow;
            victimStats.LastActive = DateTime.UtcNow;
        }

        /// <summary>
        /// Update the client stats (skill etc)
        /// </summary>
        /// <param name="clientStats">Client statistics</param>
        /// <returns></returns>
        private EFClientStatistics UpdateStats(EFClientStatistics clientStats, EFClient client)
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

            var timeSinceLastCalc = (DateTime.UtcNow - clientStats.LastStatCalculation).TotalSeconds / 60.0;

            var scoreDifference = 0;
            // this means they've been tking or suicide and is the only time they can have a negative SPM
            if (clientStats.RoundScore < 0)
            {
                scoreDifference = clientStats.RoundScore + clientStats.LastScore;
            }

            else if (clientStats.RoundScore > 0 && clientStats.LastScore < clientStats.RoundScore)
            {
                scoreDifference = clientStats.RoundScore - clientStats.LastScore;
            }

            var killSpm = scoreDifference / timeSinceLastCalc;
            var spmMultiplier = 2.934 *
                                   Math.Pow(
                                       _servers[clientStats.ServerId]
                                           .TeamCount((IW4Info.Team) clientStats.Team == IW4Info.Team.Allies
                                               ? IW4Info.Team.Axis
                                               : IW4Info.Team.Allies), -0.454);
            killSpm *= Math.Max(1, spmMultiplier);

            // update this for ac tracking
            clientStats.SessionSPM = clientStats.SessionScore / Math.Max(1, client.ConnectionLength / 60.0);

            // calculate how much the KDR should weigh
            // 1.637 is a Eddie-Generated number that weights the KDR nicely
            double currentKDR = clientStats.SessionDeaths == 0
                ? clientStats.SessionKills
                : clientStats.SessionKills / clientStats.SessionDeaths;
            double alpha = Math.Sqrt(2) / Math.Min(600, Math.Max(clientStats.Kills + clientStats.Deaths, 1));
            clientStats.RollingWeightedKDR = (alpha * currentKDR) + (1.0 - alpha) * clientStats.KDR;
            double KDRWeight = Math.Round(Math.Pow(clientStats.RollingWeightedKDR, 1.637 / Math.E), 3);

            // calculate the weight of the new play time against last 10 hours of gameplay
            int totalPlayTime = (clientStats.TimePlayed == 0)
                ? (int) (DateTime.UtcNow - clientStats.LastActive).TotalSeconds
                : clientStats.TimePlayed + (int) (DateTime.UtcNow - clientStats.LastActive).TotalSeconds;

            double SPMAgainstPlayWeight = timeSinceLastCalc / Math.Min(600, (totalPlayTime / 60.0));

            // calculate the new weight against average times the weight against play time
            clientStats.SPM = (killSpm * SPMAgainstPlayWeight) + (clientStats.SPM * (1 - SPMAgainstPlayWeight));

            if (clientStats.SPM < 0)
            {
                _log.LogWarning("clientStats SPM < 0 {scoreDifference} {@clientStats}", scoreDifference, clientStats);
                clientStats.SPM = 0;
            }

            clientStats.SPM = Math.Round(clientStats.SPM, 3);
            clientStats.Skill = Math.Round((clientStats.SPM * KDRWeight), 3);

            // fixme: how does this happen?
            if (double.IsNaN(clientStats.SPM) || double.IsNaN(clientStats.Skill))
            {
                _log.LogWarning("clientStats SPM/Skill NaN {@killInfo}",
                    new {killSPM = killSpm, KDRWeight, totalPlayTime, SPMAgainstPlayWeight, clientStats, scoreDifference});
                clientStats.SPM = 0;
                clientStats.Skill = 0;
            }

            clientStats.LastStatCalculation = DateTime.UtcNow;
            //clientStats.LastScore = clientStats.SessionScore;
            clientStats.UpdatedAt = DateTime.UtcNow;

            return clientStats;
        }

        public EFServerStatistics InitializeServerStats(long serverId)
        {
            EFServerStatistics serverStats;

            using var ctx = _contextFactory.CreateContext(enableTracking: false);
            var serverStatsSet = ctx.Set<EFServerStatistics>();
            serverStats = serverStatsSet.FirstOrDefault(s => s.ServerId == serverId);

            if (serverStats == null)
            {
                _log.LogDebug("Initializing server stats for {serverId}", serverId);
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

            return serverStats;
        }

        public void ResetKillstreaks(Server sv)
        {
            foreach (var session in sv.GetClientsAsList()
                .Select(_client => new
                {
                    stat = _client.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY),
                    detection = _client.GetAdditionalProperty<Detection>(CLIENT_DETECTIONS_KEY)
                }))
            {
                session.stat?.StartNewSession();
                session.detection?.OnMapChange();
                session.stat?.MatchData?.StartNewMatch();
            }
        }

        public void ResetStats(EFClient client)
        {
            var stats = client.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY);

            // the cached stats have not been loaded yet
            if (stats == null)
            {
                return;
            }

            stats.Kills = 0;
            stats.Deaths = 0;
            stats.SPM = 0;
            stats.Skill = 0;
            stats.TimePlayed = 0;
            stats.EloRating = 200;
        }

        public async Task AddMessageAsync(int clientId, long serverId, bool sentIngame, string message)
        {
            // the web users can have no account
            if (clientId < 1)
            {
                return;
            }

            await using var ctx = _contextFactory.CreateContext(enableTracking: false);
            ctx.Set<EFClientMessage>().Add(new EFClientMessage()
            {
                ClientId = clientId,
                Message = message,
                ServerId = serverId,
                TimeSent = DateTime.UtcNow,
                SentIngame = sentIngame
            });

            await ctx.SaveChangesAsync();
        }

        public async Task Sync(Server sv)
        {
            long serverId = GetIdForServer(sv);

            var waiter = _servers[serverId].OnSaving;
            try
            {
                await waiter.WaitAsync();

                await using var ctx = _contextFactory.CreateContext();
                var serverStatsSet = ctx.Set<EFServerStatistics>();
                serverStatsSet.Update(_servers[serverId].ServerStatistics);
                await ctx.SaveChangesAsync();

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
                _log.LogError(e, "There was a problem syncing server stats");
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

            // todo: this is not stable and will need to be migrated again...
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
