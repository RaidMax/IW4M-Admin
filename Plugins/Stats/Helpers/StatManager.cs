using IW4MAdmin.Plugins.Stats.Cheat;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
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
using Humanizer;
using IW4MAdmin.Plugins.Stats.Web.Dtos;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Stats.Client.Abstractions;
using Stats.Config;
using Stats.Helpers;
using static IW4MAdmin.Plugins.Stats.Cheat.Detection;
using EFClient = SharedLibraryCore.Database.Models.EFClient;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Plugins.Stats.Helpers
{
    public class StatManager(
        ILogger<StatManager> logger,
        IDatabaseContextFactory contextFactory,
        StatsConfiguration statsConfig,
        IServerDistributionCalculator serverDistributionCalculator,
        ILookupCache<EFServer> serverCache)
    {
        private const int MAX_CACHED_HITS = 100;
        private readonly ConcurrentDictionary<long, ServerStats> _servers = new();
        private readonly ILogger _log = logger;
        public static string CLIENT_STATS_KEY = "ClientStats";
        public static string CLIENT_DETECTIONS_KEY = "ClientDetections";
        public static string ESTIMATED_SCORE = "EstimatedScore";
        private readonly SemaphoreSlim _addPlayerWaiter = new(1, 1);
        private readonly ConcurrentDictionary<string, int> _performanceBucketIdCache = new();

        ~StatManager()
        {
            _addPlayerWaiter.Dispose();
        }

        public Expression<Func<EFRating, bool>> GetRankingFunc(long? serverId = null)
        {
            var fifteenDaysAgo = Extensions.FifteenDaysAgo();
            return (r) => r.ServerId == serverId &&
                          r.When > fifteenDaysAgo &&
                          r.RatingHistory.Client.Level != EFClient.Permission.Banned &&
                          r.Newest &&
                          r.ActivityAmount >= statsConfig.TopPlayersMinPlayTime;
        }

        /// <summary>
        /// gets a ranking across all servers for given client id
        /// </summary>
        /// <param name="clientId">client id of the player</param>
        /// <param name="serverId"></param>
        /// <param name="performanceBucket"></param>
        /// <returns></returns>
        public async Task<int> GetClientOverallRanking(int clientId, long? serverId = null, string performanceBucket = null)
        {
            await using var context = contextFactory.CreateContext(enableTracking: false);

            if (statsConfig.EnableAdvancedMetrics)
            {
                var bucketConfig = await GetBucketConfig(null, performanceBucket);

                var clientRanking = await context.Set<EFClientRankingHistory>()
                    .Where(GetNewRankingFunc(bucketConfig.RankingExpiration, bucketConfig.ClientMinPlayTime, serverId, bucketConfig.Code))
                    .Where(r => r.ClientId == clientId)
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

        private Expression<Func<EFClientRankingHistory, bool>> GetNewRankingFunc(TimeSpan oldestStat, TimeSpan minPlayTime,
            long? serverId = null, string performanceBucketCode = null)
        {
            var oldestDate = DateTime.UtcNow - oldestStat;
            // Pre-bucket-migration rankings have PerformanceBucketId == null. Treat those
            // as belonging to the "default" bucket so established servers don't lose their
            // existing top-stats history once a bucket is configured. Communities migrating
            // from older versions keep working even before they manually backfill bucket
            // FKs — both NULL-FK legacy rows and properly-tagged new rows surface together.
            var isDefaultBucket = PerformanceBucketCodes.IsDefault(performanceBucketCode);
            // Defence-in-depth normalisation: callers reaching this expression usually
            // come through GetBucketConfig (which already lower-cases via Normalize),
            // but a direct call with a capitalised user-supplied bucket would
            // otherwise filter to zero rows since the DB stores lower-case canonical.
            var normalizedBucket = PerformanceBucketCodes.Normalize(performanceBucketCode);
            return ranking => ranking.ServerId == serverId
                              && ranking.Client.Level != Data.Models.Client.EFClient.Permission.Banned
                              && ranking.CreatedDateTime >= oldestDate
                              && ranking.ZScore != null
                              && ranking.PerformanceMetric != null
                              && ranking.Newest
                              && (ranking.PerformanceBucket.Code == normalizedBucket
                                  || (isDefaultBucket && ranking.PerformanceBucketId == null))
                              && ranking.Client.TotalConnectionTime >= (int)minPlayTime.TotalSeconds;
        }

        public async Task<int> GetTotalRankedPlayers(long? serverId = null, string performanceBucket = null)
        {
            var bucketConfig = await GetBucketConfig(serverId, performanceBucket);

            await using var context = contextFactory.CreateContext(enableTracking: false);

            return await context.Set<EFClientRankingHistory>()
                .Where(GetNewRankingFunc(bucketConfig.RankingExpiration, bucketConfig.ClientMinPlayTime, serverId: serverId,
                    bucketConfig.Code))
                .CountAsync();
        }

        public class RankingSnapshot
        {
            public int ClientId { get; set; }
            public string Name { get; set; }
            public DateTime LastConnection { get; set; }
            public double? PerformanceMetric { get; set; }
            public double? ZScore { get; set; }
            public int? Ranking { get; set; }
            public DateTime CreatedDateTime { get; set; }
        }

        public async Task<(List<TopStatsInfo> Players, int RankingHistoryRowsConsumed)> GetNewTopStats(
            int start, int count, long? serverId = null, string performanceBucketCode = null)
        {
            var bucketConfig = await GetBucketConfig(serverId, performanceBucketCode);

            await using var context = contextFactory.CreateContext(false);

            // The page-fill loop: ranking history `RankedClientsCountAsync` may report N
            // rows but the per-client stats join below filters on TimePlayed/Kills/Deaths
            // — so a slice of `count` ranking rows can yield far fewer top-stats rows.
            // Without chunk-fill, the leaderboard returns short pages and infinite-scroll
            // stalls. We over-fetch ranking rows in chunks and validate against the stats
            // filter until we have `count` qualifying clients (or exhaust the source).
            // The caller advances its offset by the *ranking-history rows consumed*
            // returned in the tuple, so successive pages don't re-walk rejected rows.
            var rankingIdsQuery = context.Set<EFClientRankingHistory>()
                .Where(GetNewRankingFunc(bucketConfig.RankingExpiration, bucketConfig.ClientMinPlayTime, serverId: serverId,
                    bucketConfig.Code))
                .OrderByDescending(ranking => ranking.PerformanceMetric)
                .Select(ranking => ranking.ClientId);

            var clientIdsList = new List<int>(count);
            var consumed = 0;
            // Chunk size = 2× requested page; small enough to stay cheap on each round-
            // trip, large enough to amortize the round-trip cost when filter ratio is low.
            var chunkSize = Math.Max(count * 2, 50);
            // Hard cap iterations to avoid an unbounded loop on pathological data
            // (e.g. a bucket where every single ranked client fails the stats filter).
            var attempts = 0;
            const int maxAttempts = 20;

            while (clientIdsList.Count < count && attempts++ < maxAttempts)
            {
                var chunk = await rankingIdsQuery
                    .Skip(start + consumed)
                    .Take(chunkSize)
                    .ToListAsync();
                if (chunk.Count == 0) break;

                var validIds = (await context.Set<EFClientStatistics>()
                        .Where(stat => chunk.Contains(stat.ClientId))
                        .Where(stat => stat.TimePlayed > 0)
                        .Where(stat => stat.Kills > 0 || stat.Deaths > 0)
                        .Where(stat => serverId == null || stat.ServerId == serverId)
                        .Select(stat => stat.ClientId)
                        .Distinct()
                        .ToListAsync())
                    .ToHashSet();

                // Preserve original ranking order — `chunk` is already
                // performance-descending — so the appended results stay sorted.
                var lastWalkedToIdx = -1;
                var filledMidChunk = false;
                for (var i = 0; i < chunk.Count; i++)
                {
                    var id = chunk[i];
                    if (!validIds.Contains(id)) continue;
                    if (clientIdsList.Contains(id)) continue;
                    clientIdsList.Add(id);
                    lastWalkedToIdx = i;
                    if (clientIdsList.Count >= count)
                    {
                        filledMidChunk = true;
                        break;
                    }
                }

                // Advance `consumed` by what we actually walked, NOT by the whole
                // chunk. If we filled the page mid-chunk, items past `lastWalkedToIdx`
                // were never inspected — they belong to the next page. Eating the
                // whole chunk here drops `chunk.Count - (lastWalkedToIdx + 1)` rows
                // from the leaderboard (caller advances offset by `consumed`, and
                // anything counted here is permanently skipped). Symptom was
                // infinite-scroll terminating ~25 short of TotalRankedClients on
                // every bucket: page 1 ate positions 0-49 to return 25 visible,
                // page 2 ate 50-99 to return 25 more, but positions 25-49 and 75-99
                // never made it onscreen because consumed jumped past them.
                //
                // If we walked the whole chunk without filling (low validity ratio
                // OR end-of-source short chunk), every position WAS inspected, so
                // advance by chunk.Count.
                consumed += filledMidChunk ? lastWalkedToIdx + 1 : chunk.Count;

                // Source exhausted: chunk smaller than requested means no more rows
                // beyond this slice. Stop even if we didn't fill the page.
                if (chunk.Count < chunkSize) break;
            }

            var rankingsDict = new Dictionary<int, List<RankingSnapshot>>();

            var includeNullBucket = PerformanceBucketCodes.IsDefault(bucketConfig.Code);
            foreach (var clientId in clientIdsList)
            {
                var eachRank = await context.Set<EFClientRankingHistory>()
                    .Where(ranking => ranking.ClientId == clientId)
                    .Where(ranking => ranking.ServerId == serverId)
                    .Where(ranking => ranking.PerformanceBucket.Code == bucketConfig.Code
                                      || (includeNullBucket && ranking.PerformanceBucketId == null))
                    .OrderByDescending(ranking => ranking.CreatedDateTime)
                    .Select(ranking => new RankingSnapshot
                    {
                        ClientId = ranking.ClientId,
                        Name = ranking.Client.CurrentAlias.Name,
                        LastConnection = ranking.Client.LastConnection,
                        PerformanceMetric = ranking.PerformanceMetric,
                        ZScore = ranking.ZScore,
                        Ranking = ranking.Ranking,
                        CreatedDateTime = ranking.CreatedDateTime
                    })
                    .Take(60)
                    .ToListAsync();

                if (!rankingsDict.TryAdd(clientId, eachRank))
                {
                    rankingsDict[clientId] = rankingsDict[clientId].Concat(eachRank).Distinct()
                        .OrderByDescending(ranking => ranking.CreatedDateTime).ToList();
                }
            }

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
                    KDR = s.Sum(c => (c.Kills / (double)(c.Deaths == 0 ? 1 : c.Deaths)) * c.TimePlayed) /
                          s.Sum(c => c.TimePlayed),
                    TotalTimePlayed = s.Sum(c => c.TimePlayed),
                    UpdatedAt = s.Max(c => c.UpdatedAt)
                })
                .ToListAsync();

            var finished = statsInfo
                .Where(stat => rankingsDict[stat.ClientId].Any())
                .OrderByDescending(stat => rankingsDict[stat.ClientId].First().PerformanceMetric)
                .Select((s, index) => new TopStatsInfo
                {
                    ClientId = s.ClientId,
                    Id = (int?)serverId ?? 0,
                    Deaths = s.Deaths,
                    Kills = s.Kills,
                    KDR = Math.Round(s.KDR, 2),
                    LastSeen = (DateTime.UtcNow - (s.UpdatedAt ?? rankingsDict[s.ClientId].First().LastConnection))
                        .HumanizeForCurrentCulture(1, TimeUnit.Week, TimeUnit.Second, ","),
                    LastSeenValue = DateTime.UtcNow - (s.UpdatedAt ?? rankingsDict[s.ClientId].First().LastConnection),
                    Name = rankingsDict[s.ClientId].First().Name,
                    Performance = Math.Round(rankingsDict[s.ClientId].First().PerformanceMetric ?? 0, 2),
                    RatingChange = (rankingsDict[s.ClientId].Last().Ranking -
                                    rankingsDict[s.ClientId].First().Ranking) ?? 0,
                    PerformanceHistory = rankingsDict[s.ClientId].Select(ranking => new PerformanceHistory
                            { Performance = ranking.PerformanceMetric ?? 0, OccurredAt = ranking.CreatedDateTime })
                        .ToList(),
                    TimePlayed = Math.Round(s.TotalTimePlayed / 3600.0, 1).ToString("#,##0"),
                    TimePlayedValue = TimeSpan.FromSeconds(s.TotalTimePlayed),
                    Ranking = index + start + 1,
                    ZScore = rankingsDict[s.ClientId].First().ZScore,
                    ServerId = serverId
                })
                .OrderBy(r => r.Ranking)
                .ToList();

            // Run typed-field transformers BEFORE the metric-row loop below so any
            // premium override of Kills/Deaths/KDR (e.g. zombies bucket sourcing
            // from EFZombieClientStatAggregates instead of bridged EFClientStatistics)
            // flows into the displayed metric values without us having to re-sync.
            // Single source of truth: the typed DTO fields. CustomStatsMetrics still
            // runs after the metric loop and can append additional rows.
            foreach (var transformer in Plugin.ServerManager.CustomTopStatsTransformers)
            {
                await transformer(finished.Cast<ITopStatsMutable>().ToList(), serverId, bucketConfig.Code);
            }

            // Zombies bucket: suppress KDR row. Kills scale ~round^2 and deaths floor at 1
            // in zombies, so K/D is mathematically broken as a skill signal — the premium
            // plugin appends RPD (rounds-per-down) below as the canonical survival ratio.
            var suppressKdr = string.Equals(bucketConfig.Code, "zombies", StringComparison.OrdinalIgnoreCase);

            foreach (var topStatsInfo in finished)
            {
                topStatsInfo.Metrics.Add(new EFMeta
                {
                    Extra = "Kills",
                    Value = topStatsInfo.Kills.ToNumericalString(),
                    Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_KILLS"]
                });
                topStatsInfo.Metrics.Add(new EFMeta
                {
                    Extra = "Deaths",
                    Value = topStatsInfo.Deaths.ToNumericalString(),
                    Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_DEATHS"]
                });
                if (!suppressKdr)
                {
                    topStatsInfo.Metrics.Add(new EFMeta
                    {
                        Extra = "KDR",
                        Value = topStatsInfo.KDR.ToNumericalString(),
                        Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_KDR"]
                    });
                }
                topStatsInfo.Metrics.Add(new EFMeta
                {
                    Extra = "TimePlayed",
                    Value = topStatsInfo.TimePlayedValue.HumanizeForCurrentCulture(),
                    Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_PLAYER"]
                });
                topStatsInfo.Metrics.Add(new EFMeta
                {
                    Extra = "LastSeen",
                    Value = topStatsInfo.LastSeenValue.HumanizeForCurrentCulture(),
                    Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_LSEEN"]
                });
            }

            foreach (var customMetricFunc in Plugin.ServerManager.CustomStatsMetrics)
            {
                await customMetricFunc(finished.ToDictionary(kvp => kvp.ClientId, kvp => kvp.Metrics), serverId,
                    bucketConfig.Code, true);
            }

            return (finished, consumed);
        }

        /// <summary>
        /// Resolves the <see cref="PerformanceBucketConfiguration"/> for a server or bucket code.
        /// Resolution order: explicit bucket code → config match → DB lookup by serverId → defaults.
        /// Returns default config (global min-playtime / 15-day expiry) when no bucket applies.
        /// </summary>
        public async Task<PerformanceBucketConfiguration> GetBucketConfig(long? serverId = null,
            string performanceBucketCode = null)
        {
            // Returned Code is ALWAYS the canonical normalised form (lower-cased,
            // null/empty collapsed to "default"). Downstream consumers use Code
            // as a cache key, DB filter value, and FK lookup token — letting an
            // un-normalised Code escape historically caused cache mismatches
            // (e.g. PerformanceBucketCode = "Zombies" in IW4MAdminSettings vs
            // "zombies" in the DB; null vs "" vs "default" all meaning the same
            // logical pool). Centralising normalisation here means callers can
            // treat Code as the truth.
            PerformanceBucketConfiguration BuildConfig(string sourceCode)
            {
                var configured = !string.IsNullOrEmpty(sourceCode)
                    ? statsConfig.PerformanceBuckets.FirstOrDefault(bucket =>
                        string.Equals(bucket.Code, sourceCode, StringComparison.OrdinalIgnoreCase))
                    : null;

                var result = configured is not null
                    ? new PerformanceBucketConfiguration
                    {
                        ClientMinPlayTime = configured.ClientMinPlayTime,
                        RankingExpiration = configured.RankingExpiration
                    }
                    : new PerformanceBucketConfiguration
                    {
                        ClientMinPlayTime = TimeSpan.FromSeconds(statsConfig.TopPlayersMinPlayTime),
                        RankingExpiration = TimeSpan.FromDays(15)
                    };

                result.Code = PerformanceBucketCodes.Normalize(sourceCode);
                return result;
            }

            // Explicit caller-supplied code wins over server's DB-recorded code.
            if (performanceBucketCode is not null)
            {
                return BuildConfig(performanceBucketCode);
            }

            // No serverId hint either → caller wants the default bucket.
            if (serverId is null)
            {
                return BuildConfig(null);
            }

            // The server cache doesn't eagerly load the PerformanceBucket navigation,
            // so we query the database directly for the bucket code.
            await using var context = contextFactory.CreateContext(false);
            var cachedServer = await serverCache.FirstAsync(server => server.Id == serverId);
            var dbBucketCode = cachedServer is null
                ? null
                : await context.Set<Data.Models.Client.Stats.EFPerformanceBucket>()
                    .Where(b => b.PerformanceBucketId == cachedServer.PerformanceBucketId)
                    .Select(b => b.Code)
                    .FirstOrDefaultAsync();

            return BuildConfig(dbBucketCode);
        }

        public async Task<List<TopStatsInfo>> GetTopStats(int start, int count, long? serverId = null, string performanceBucket = null)
        {
            if (statsConfig.EnableAdvancedMetrics)
            {
                var (players, _) = await GetNewTopStats(start, count, serverId, performanceBucket);
                return players;
            }

            await using var context = contextFactory.CreateContext(enableTracking: false);
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
                    Ratings = grp.Select(r => new { r.Performance, r.Ranking, r.When })
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
                    KDR = s.Sum(c => (c.Kills / (double)(c.Deaths == 0 ? 1 : c.Deaths)) * c.TimePlayed) /
                          s.Sum(c => c.TimePlayed),
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
                    LastSeen = (DateTime.UtcNow - clientRatingsDict[s.ClientId].LastConnection)
                        .HumanizeForCurrentCulture(),
                    LastSeenValue = DateTime.UtcNow - clientRatingsDict[s.ClientId].LastConnection,
                    Name = clientRatingsDict[s.ClientId].Name,
                    Performance = Math.Round(clientRatingsDict[s.ClientId].Performance, 2),
                    RatingChange = ratingInfo.First(r => r.Key == s.ClientId).Ratings.First().Ranking -
                                   ratingInfo.First(r => r.Key == s.ClientId).Ratings.Last().Ranking,
                    PerformanceHistory = ratingInfo.First(r => r.Key == s.ClientId).Ratings.Count() > 1
                        ? ratingInfo.First(r => r.Key == s.ClientId).Ratings.OrderBy(r => r.When)
                            .Select(r => new PerformanceHistory { Performance = r.Performance, OccurredAt = r.When })
                            .ToList()
                        : new List<PerformanceHistory>
                        {
                            new()
                            {
                                Performance = clientRatingsDict[s.ClientId].Performance, OccurredAt = DateTime.UtcNow
                            },
                            new()
                            {
                                Performance = clientRatingsDict[s.ClientId].Performance, OccurredAt = DateTime.UtcNow
                            }
                        },
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

        public async Task EnsureServerAdded(IGameServer gameServer, CancellationToken token)
        {
            try
            {
                // check to see if the stats have ever been initialized
                var cachedServer =
                    await serverCache.FirstAsync(cachedServer => cachedServer.EndPoint == gameServer.Id);

                if (cachedServer == null)
                {
                    _log.LogError("[Stats::EnsureServerAdded] cachedServer is null for endpoint {Endpoint}", gameServer.Id);
                    return;
                }

                var serverStats = InitializeServerStats(gameServer.LegacyDatabaseId);

                _servers.TryAdd(cachedServer.ServerId, new ServerStats(cachedServer, serverStats, gameServer as Server)
                {
                    IsTeamBased = gameServer.Gametype != "dm"
                });
            }

            catch (Exception ex)
            {
                _log.LogError(ex, "{Message}",
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
                var serverId = (pl.CurrentServer as IGameServer).LegacyDatabaseId;

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

                await using var ctx = contextFactory.CreateContext(enableTracking: false);
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
                                Location = (int)hl
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
                            Location = (int)hl
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

                pl.SetAdditionalProperty(CLIENT_DETECTIONS_KEY, new Detection(_log, clientStats, statsConfig));
                _log.LogDebug("Added {client} to stats", pl.ToString());

                return clientStats;
            }

            catch (DbUpdateException updateException) when (
                updateException.InnerException is DbException { SqlState: "23503" } or SqliteException { SqliteErrorCode: 787 })
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
        /// <param name="client">Disconnecting client</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task RemovePlayer(EFClient client, CancellationToken cancellationToken)
        {
            _log.LogDebug("Removing {Client} from stats", client.ToString());

            if (client.CurrentServer == null)
            {
                _log.LogWarning("Disconnecting client {Client} is not on a server", client.ToString());
                return;
            }

            var serverId = (client.CurrentServer as IGameServer).LegacyDatabaseId;
            var serverStats = _servers[serverId].ServerStatistics;

            // get individual client's stats
            var clientStats = client.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY);
            // sync their stats before they leave
            if (clientStats != null)
            {
                clientStats = UpdateStats(clientStats, client);
                await SaveClientStats(clientStats);
                if (statsConfig.EnableAdvancedMetrics)
                {
                    await UpdateHistoricalRanking(client.ClientId, clientStats, serverId);
                }

                // increment the total play time
                serverStats.TotalPlayTime += client.ConnectionLength;
                client.SetAdditionalProperty(CLIENT_STATS_KEY, null);
            }

            else
            {
                _log.LogWarning("Disconnecting client {Client} has not been added to stats", client.ToString());
            }
        }

        private async Task SaveClientStats(EFClientStatistics clientStats)
        {
            await using var ctx = contextFactory.CreateContext();
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

                EFClientKill hit;
                try
                {
                    hit = new EFClientKill
                    {
                        Active = true,
                        AttackerId = attacker.ClientId,
                        VictimId = victim.ClientId,
                        ServerId = serverId,
                        DeathOrigin = vDeathOrigin,
                        KillOrigin = vKillOrigin,
                        DeathType = (int)ParseEnum<IW4Info.MeansOfDeath>.Get(type, typeof(IW4Info.MeansOfDeath)),
                        Damage = int.Parse(damage),
                        HitLoc = (int)ParseEnum<IW4Info.HitLocation>.Get(hitLoc, typeof(IW4Info.HitLocation)),
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
                        GameName = (int)attacker.CurrentServer.GameName
                    };
                }
                catch (Exception ex)
                {
                    _log.LogError(ex,
                        "Could not parse script hit data. Damage={Damage}, TimeOffset={Offset}, TimeSinceLastAttack={LastAttackTime}",
                        damage, offset, lastAttackTime);

                    return;
                }

                hit.SetAdditionalProperty("HitLocationReference", hitLoc);

                if (hit.HitLoc == (int)IW4Info.HitLocation.shield)
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
                if (hit.DeathType == (int)IW4Info.MeansOfDeath.MOD_PISTOL_BULLET ||
                    hit.DeathType == (int)IW4Info.MeansOfDeath.MOD_RIFLE_BULLET ||
                    hit.DeathType == (int)IW4Info.MeansOfDeath.MOD_HEAD_SHOT)
                {
                    clientStats.HitLocations.First(hl => hl.Location == hit.HitLoc).HitCount += 1;
                }

                if (hit.IsKillstreakKill)
                {
                    return;
                }

                if (statsConfig.StoreClientKills)
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

                if (statsConfig.AnticheatConfiguration.Enable && !attacker.IsBot &&
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
                _log.LogError(ex, "Could not save hit or anti-cheat info {Attacker} {Victim} {Server}", attacker,
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
            await using var ctx = contextFactory.CreateContext(enableTracking: false);
            var server = _servers[serverId];
            ctx.AddRange(server.HitCache.ToList());
            await ctx.SaveChangesAsync();
            server.HitCache.Clear();
        }

        private bool ShouldUseDetection(Server server, DetectionType detectionType, long clientId)
        {
#pragma warning disable CS0612
            var serverDetectionTypes = statsConfig.AnticheatConfiguration.ServerDetectionTypes;
#pragma warning restore CS0612
            var gameDetectionTypes = statsConfig.AnticheatConfiguration.GameDetectionTypes;
            var ignoredClients = statsConfig.AnticheatConfiguration.IgnoredClientIds;

            if (ignoredClients.Contains(clientId))
            {
                return false;
            }

            try
            {
                if (!serverDetectionTypes[server.EndPoint].Contains(detectionType))
                {
                    return false;
                }
            }

            catch (KeyNotFoundException)
            {
            }

            try
            {
                if (!gameDetectionTypes[server.GameName].Contains(detectionType))
                {
                    return false;
                }
            }
            catch
            {
                // ignored
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
                                ? $"{penalty.Type}-{(int)penalty.Location}-{Math.Round(penalty.Value, 2)}@{penalty.HitCount}"
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
                        ? $"{penalty.Type}-{(int)penalty.Location}-{Math.Round(penalty.Value, 2)}@{penalty.HitCount}"
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

            await using var ctx = contextFactory.CreateContext();
            while ((change = clientDetection.Tracker.GetNextChange()) != default(EFACSnapshot))
            {
                ctx.Add(change);
            }

            await ctx.SaveChangesAsync();
        }

        public async Task AddStandardKill(EFClient attacker, EFClient victim)
        {
            var serverId = (attacker.CurrentServer as IGameServer).LegacyDatabaseId;

            var attackerStats = attacker.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY);
            var victimStats = victim.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY);

            // update the total stats
            _servers[serverId].ServerStatistics.TotalKills += 1;

            if (attackerStats == null)
            {
                _log.LogWarning("Stats for {Client} are not yet initialized", attacker.ToString());
                return;
            }

            // Victim may be a non-player entity (e.g. zombie) with no stats initialized
            if (victimStats == null)
            {
                if (attackerStats.ClientId == victim.ClientId)
                {
                    return;
                }

                // Credit the attacker for the kill (PvE scenario like zombies)
                attackerStats.Kills += 1;
                attackerStats.MatchData.Kills += 1;
                attackerStats.SessionKills += 1;
                attackerStats.KillStreak += 1;
                attackerStats.DeathStreak = 0;

                // Update score tracking so SPM/Skill calculations work
                var estimatedScore = attacker.Score;
                attackerStats.SessionScore = estimatedScore;
                attacker.SetAdditionalProperty(ESTIMATED_SCORE, estimatedScore);

                attackerStats = UpdateStats(attackerStats, attacker);
                attackerStats.LastScore = estimatedScore;
                attackerStats.TimePlayed += (int)(DateTime.UtcNow - attackerStats.LastActive).TotalSeconds;
                attackerStats.LastActive = DateTime.UtcNow;

                if (double.IsNaN(attackerStats.SPM) || double.IsNaN(attackerStats.Skill) || double.IsInfinity(attackerStats.Skill))
                {
                    attackerStats.SPM = 0.0;
                    attackerStats.Skill = 0.0;
                }

                // update ranking on the same interval as PvP
                if ((DateTime.UtcNow - attackerStats.LastStatHistoryUpdate).TotalMinutes >=
                    (Utilities.IsDevelopment ? 0.5 : statsConfig.EnableAdvancedMetrics ? 5.0 : 2.5))
                {
                    try
                    {
                        await attackerStats.ProcessingHit.WaitAsync(Utilities.DefaultCommandTimeout,
                            Plugin.ServerManager.CancellationToken);
                        if (statsConfig.EnableAdvancedMetrics)
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
            var streakMessage = attacker.CurrentServer.IsZombieServer()
                ? string.Empty
                : attackerStats.ClientId != victimStats.ClientId
                    ? StreakMessage.MessageOnStreak(attackerStats.KillStreak, attackerStats.DeathStreak, statsConfig)
                    : StreakMessage.MessageOnStreak(-1, -1, statsConfig);

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
                (Utilities.IsDevelopment ? 0.5 : statsConfig.EnableAdvancedMetrics ? 5.0 : 2.5))
            {
                try
                {
                    // kill event is not designated as blocking, so we should be able to enter and exit
                    // we need to make this thread safe because we can potentially have kills qualify
                    // for stat history update, but one is already processing that invalidates the original
                    await attackerStats.ProcessingHit.WaitAsync(Utilities.DefaultCommandTimeout,
                        Plugin.ServerManager.CancellationToken);
                    if (statsConfig.EnableAdvancedMetrics)
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
            int currentSessionTime = (int)(DateTime.UtcNow - client.LastConnection).TotalSeconds;

            // don't update their stat history if they haven't played long
            if (currentSessionTime < 60)
            {
                return;
            }

            int currentServerTotalPlaytime = clientStats.TimePlayed + currentSessionTime;

            await using var ctx = contextFactory.CreateContext(enableTracking: true);
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

        /// <summary>
        /// Creates a point-in-time ranking snapshot for the client within their server's performance bucket.
        /// Aggregates stats across all servers sharing the same bucket, weights Z-scores by playtime,
        /// converts to a percentile-based performance metric, and persists as <see cref="EFClientRankingHistory"/>.
        /// Only includes stats from servers in the same bucket that meet min-playtime and recency thresholds.
        /// </summary>
        public async Task UpdateHistoricalRanking(int clientId, EFClientStatistics clientStats, long serverId)
        {
            var bucketConfig = await GetBucketConfig(serverId);

            await using var context = contextFactory.CreateContext();
            var oldestStateDate = DateTime.UtcNow - bucketConfig.RankingExpiration;
            // Match the rest of the bucket-tolerance story: a server with no
            // PerformanceBucketId FK (the universal post-update state for
            // communities upgrading to this version — IW4MAdminSettings rarely
            // sets PerformanceBucketCode on day one) is implicitly part of the
            // default bucket. Without the OR-NULL fallback this query returned
            // zero cross-server rows for default-bucket aggregation, so each
            // kill rolled up only the current server's stats — leaderboard
            // ratings reflected one server, not the player's combined skill.
            var isDefaultBucket = PerformanceBucketCodes.IsDefault(bucketConfig.Code);
            var performances = await context.Set<EFClientStatistics>()
                .AsNoTracking()
                .Include(stat => stat.Server)
                .Where(stat => stat.ClientId == clientId)
                .Where(stat => stat.ServerId != serverId) // ignore the one we're currently tracking
                .Where(stat => stat.Server.PerformanceBucket.Code == bucketConfig.Code
                               || (isDefaultBucket && stat.Server.PerformanceBucketId == null))
                .Where(stats => stats.UpdatedAt >= oldestStateDate)
                .Where(stats => stats.TimePlayed >= (int)bucketConfig.ClientMinPlayTime.TotalSeconds)
                .ToListAsync();

            if (clientStats.TimePlayed >= bucketConfig.ClientMinPlayTime.TotalSeconds)
            {
                await UpdateForServer(clientId, clientStats, context, (int)bucketConfig.ClientMinPlayTime.TotalSeconds,
                    bucketConfig.RankingExpiration, serverId, bucketConfig.Code);
                clientStats.Server = await serverCache.FirstAsync(server => server.Id == serverId);
                performances.Add(clientStats);
            }

            if (performances.Any(performance => performance.TimePlayed >= (int)bucketConfig.ClientMinPlayTime.TotalSeconds))
            {
                await UpdateAggregateForServerOrBucket(clientId, clientStats, context, performances, bucketConfig);
            }
        }

        private async Task UpdateAggregateForServerOrBucket(int clientId, EFClientStatistics clientStats, DatabaseContext context,
            List<EFClientStatistics> performances, PerformanceBucketConfiguration bucketConfig)
        {
            // Compute the client's bucket-aggregate z-score from raw weighted Performance
            // against the BUCKET log-normal distribution.
            //
            // Why not the previous approach (playtime-weighted average of EFClientStatistics.ZScore)?
            // The ZScore column on EFClientStatistics is fitted per-server. Z-scores from
            // different distributions are in different units and don't compose under
            // averaging — combining them across the bucket's servers conflates unrelated
            // scales. Concretely, on a low-population server (n=2 players with similar
            // skill) the fitted sigma collapses toward 0, producing per-row z-scores in
            // the hundreds. Averaging those into the bucket aggregate then poisoned the
            // bucket-wide max, which became the rating denominator — every other player's
            // rating compressed to single-digit percent of the 0-1000 range.
            //
            // The corrected model: take the playtime-weighted Performance composite
            // (already a meaningful per-client scalar), transform once through the
            // bucket-fitted log-normal. One distribution, one transform, every aggregate
            // z-score directly comparable to every other within the bucket.
            var totalPlaytime = performances.Sum(p => p.TimePlayed);
            if (totalPlaytime <= 0)
            {
                return;
            }

            var weightedPerformance =
                performances.Sum(p => p.Performance * p.TimePlayed) / (double)totalPlaytime;

            var aggregateZScore = await serverDistributionCalculator.GetZScoreForServerOrBucket(
                weightedPerformance, performanceBucket: bucketConfig.Code);

            // Rank by raw weighted Performance — this is equivalent to ranking by aggregate
            // z-score (the log-normal transform is monotonically increasing in Performance),
            // and lets the comparison run entirely server-side without re-deriving z's per
            // candidate row. Same default-bucket OR-clause as GetNewRankingFunc so legacy
            // NULL-FK rows participate alongside properly-tagged new rows.
            var isDefaultBucket = PerformanceBucketCodes.IsDefault(bucketConfig.Code);
            var aggregateRanking = await context.Set<EFClientStatistics>()
                .Where(stat => stat.ClientId != clientId)
                .Where(stat => stat.Server.PerformanceBucket.Code == bucketConfig.Code
                               || (isDefaultBucket && stat.Server.PerformanceBucketId == null))
                .Where(AdvancedClientStatsResourceQueryHelper.GetRankingFunc(
                    (int)bucketConfig.ClientMinPlayTime.TotalSeconds, bucketConfig.RankingExpiration))
                .GroupBy(stat => stat.ClientId)
                .Where(group => group.Sum(stat => (stat.EloRating / 3.0 + stat.Skill * 2.0 / 3.0) * stat.TimePlayed)
                                / group.Sum(stat => stat.TimePlayed) > weightedPerformance)
                .Select(c => c.Key)
                .CountAsync();

            var newPerformanceMetric = await serverDistributionCalculator.GetRatingForZScore(aggregateZScore, bucketConfig.Code);

            if (newPerformanceMetric == null)
            {
                _log.LogWarning("Could not determine performance metric for {Client} {AggregateZScore}",
                    clientStats.Client?.ToString(), aggregateZScore);
                return;
            }

            var performanceBucketId = await GetOrCachePerformanceBucketId(context, bucketConfig.Code);

            var aggregateRankingSnapshot = new EFClientRankingHistory
            {
                ClientId = clientId,
                ZScore = aggregateZScore,
                Ranking = aggregateRanking,
                PerformanceMetric = newPerformanceMetric,
                PerformanceBucketId = performanceBucketId,
                Newest = true,
            };

            context.Add(aggregateRankingSnapshot);

            await PruneOldRankings(context, clientId, performanceBucketCode: bucketConfig.Code);
            await context.SaveChangesAsync();
        }

        private async Task UpdateForServer(int clientId, EFClientStatistics clientStats, DatabaseContext context,
            int minPlayTime, TimeSpan oldestStat, long? serverId = null, string performanceBucketCode = null)
        {
            clientStats.ZScore =
                await serverDistributionCalculator.GetZScoreForServerOrBucket(clientStats.Performance, serverId);

            // Persist ZScore back to EFClientStatistics so the distribution/maxZScore caches
            // can find non-zero values on subsequent refreshes
            await context.Set<EFClientStatistics>()
                .Where(s => s.ClientId == clientStats.ClientId && s.ServerId == clientStats.ServerId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ZScore, clientStats.ZScore));

            var serverRanking = await context.Set<EFClientStatistics>()
                .Where(stats => stats.ClientId != clientStats.ClientId)
                .Where(AdvancedClientStatsResourceQueryHelper.GetRankingFunc(minPlayTime, oldestStat,
                    clientStats.ZScore, serverId))
                .CountAsync();

            int? performanceBucketId = !string.IsNullOrEmpty(performanceBucketCode)
                ? await GetOrCachePerformanceBucketId(context, performanceBucketCode)
                : null;

            var serverRankingSnapshot = new EFClientRankingHistory
            {
                ClientId = clientId,
                ServerId = serverId,
                ZScore = clientStats.ZScore,
                Ranking = serverRanking,
                PerformanceMetric = clientStats.Performance,
                PerformanceBucketId = performanceBucketId,
                Newest = true
            };

            context.Add(serverRankingSnapshot);
            await PruneOldRankings(context, clientId, serverId, performanceBucketCode);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Resolves a bucket code to its database ID, caching the result for the process lifetime
        /// to avoid repeated DB lookups on every ranking update.
        /// </summary>
        private async Task<int?> GetOrCachePerformanceBucketId(DatabaseContext context, string bucketCode)
        {
            if (string.IsNullOrEmpty(bucketCode))
            {
                return null;
            }

            if (_performanceBucketIdCache.TryGetValue(bucketCode, out var cachedId))
            {
                return cachedId;
            }

            var bucketId = (await context.PerformanceBuckets
                .FirstOrDefaultAsync(x => x.Code == bucketCode))?.PerformanceBucketId;

            if (bucketId.HasValue)
            {
                _performanceBucketIdCache.TryAdd(bucketCode, bucketId.Value);
            }

            return bucketId;
        }

        /// <summary>
        /// Caps ranking history entries per client/server/bucket combination to avoid unbounded growth.
        /// Marks the previous "newest" entry as historical, then deletes the oldest entry when the
        /// count exceeds <c>maxRankingCount</c> (1728 ≈ 3 days at one sample every 2.5 minutes).
        /// </summary>
        private async Task PruneOldRankings(DatabaseContext context, int clientId, long? serverId = null,
            string performanceBucketCode = null)
        {
            // Pre-bucket rankings have PerformanceBucketId == null — treat them as
            // part of the default bucket so the transition to bucketed ranking doesn't
            // leave two Newest=true rows per client/server.
            var includeNullBucket = PerformanceBucketCodes.IsDefault(performanceBucketCode);
            // Defence-in-depth: DB stores Code lower-cased; normalise here so a
            // capitalised caller can't silently produce 0-row prunes (would leave
            // ranking history growing unbounded).
            var normalizedBucket = PerformanceBucketCodes.Normalize(performanceBucketCode);

            var totalRankingEntries = await context.Set<EFClientRankingHistory>()
                .Where(r => r.ClientId == clientId)
                .Where(r => r.ServerId == serverId)
                .Where(r => r.PerformanceBucket.Code == normalizedBucket
                            || (includeNullBucket && r.PerformanceBucketId == null))
                .CountAsync();

            var staleNewest = await context.Set<EFClientRankingHistory>()
                .Where(r => r.ClientId == clientId)
                .Where(r => r.ServerId == serverId)
                .Where(r => r.PerformanceBucket.Code == normalizedBucket
                            || (includeNullBucket && r.PerformanceBucketId == null))
                .Where(r => r.Newest)
                .ToListAsync();

            foreach (var stale in staleNewest)
            {
                stale.Newest = false;
                context.Update(stale);
            }

            const int maxRankingCount = 1728; // 60 / 2.5 * 24 * 3 ( 3 days at sample every 2.5 minutes)

            if (totalRankingEntries > maxRankingCount)
            {
                var lastRating = await context.Set<EFClientRankingHistory>()
                    .Where(r => r.ClientId == clientId)
                    .Where(r => r.ServerId == serverId)
                    .Where(r => r.PerformanceBucket.Code == normalizedBucket
                                || (includeNullBucket && r.PerformanceBucketId == null))
                    .OrderBy(r => r.CreatedDateTime)
                    .FirstOrDefaultAsync();

                if (lastRating is not null)
                {
                    context.Remove(lastRating);
                }
            }
        }

        /// <summary>
        /// Performs the incrementation of kills and deaths for client statistics
        /// </summary>
        /// <param name="attackerStats">Stats of the attacker</param>
        /// <param name="victimStats">Stats of the victim</param>
        /// <param name="attacker">Attacker</param>
        /// <param name="victim">Victim</param>
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

            var attackerEloRatingFunc =
                attacker.GetAdditionalProperty<Func<EFClient, EFClientStatistics, double>>("EloRatingFunction");

            attackerStats.EloRating =
                attackerEloRatingFunc?.Invoke(attacker, attackerStats) ?? attackerStats.EloRating;

            // Unused? New code. TODO: Check if needed?
            //var victimEloRatingFunc =
            //    victim.GetAdditionalProperty<Func<EFClient, EFClientStatistics, double>>("EloRatingFunction");

            victimStats.EloRating =
                attackerEloRatingFunc?.Invoke(victim, victimStats) ?? victimStats.EloRating;

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
                                        .TeamCount((IW4Info.Team)clientStats.Team == IW4Info.Team.Allies
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
                ? (int)(DateTime.UtcNow - clientStats.LastActive).TotalSeconds
                : clientStats.TimePlayed + (int)(DateTime.UtcNow - clientStats.LastActive).TotalSeconds;

            double SPMAgainstPlayWeight = totalPlayTime == 0 ? killSpm : timeSinceLastCalc / Math.Min(600, (totalPlayTime / 60.0));

            // calculate the new weight against average times the weight against play time
            clientStats.SPM = (killSpm * SPMAgainstPlayWeight) + (clientStats.SPM * (1 - SPMAgainstPlayWeight));

            if (clientStats.SPM < 0)
            {
                _log.LogWarning("clientStats SPM < 0 {scoreDifference} {@clientStats}", scoreDifference, clientStats);
                clientStats.SPM = 0;
            }

            clientStats.SPM = Math.Round(clientStats.SPM, 3);
            var stdSkill = Math.Round((clientStats.SPM * KDRWeight), 3);

            var skillFunc =
                client.GetAdditionalProperty<Func<EFClient, EFClientStatistics, double>>("SkillFunction");
            if (skillFunc is not null)
            {
                clientStats.Skill = Math.Round(skillFunc(client, clientStats), 3);
            }
            else if (stdSkill > 20000)
            {
                // Phase 1.5 zombie skill-leak: result-shape gate (mode-independent).
                // MP's natural Skill ceiling on HGM was 12106 (per-bucket query
                // 2026-05-07); std formula × zombie-shaped KDR produces 50k-900k.
                // The earlier IsZombieServer() gate hid the leak from itself when
                // gametype was stale at calc-time, so gate on the output instead.
                // Write-gate stops the bleed; log captures full context to identify
                // which precondition fails (CurrentServer null / Gametype stale /
                // SkillFunction lost / different EFClient instance).
                var leakServer = client.CurrentServer;
                var leakFlag = $"ZmLog_Leak_{leakServer?.LegacyDatabaseId ?? 0}";
                if (!client.GetAdditionalProperty<bool>(leakFlag))
                {
                    client.SetAdditionalProperty(leakFlag, true);
                    _log.LogWarning(
                        "ZombieSkillLeak: client={Name}({ClientId}) server={Server} game={Game} gametype={Gametype} map={Map} isZombie={IsZombie} stdSkill={StdSkill} kills={Kills} deaths={Deaths} spm={SPM} kdrWeight={KDRWeight}",
                        client.Name, client.ClientId, leakServer?.ServerName, leakServer?.GameCode,
                        leakServer?.Gametype, leakServer?.CurrentMap?.Name, leakServer?.IsZombieServer(),
                        stdSkill, clientStats.Kills, clientStats.Deaths, clientStats.SPM, KDRWeight);
                }
                // leave clientStats.Skill unchanged — don't pollute further
            }
            else
            {
                clientStats.Skill = stdSkill;
            }

            // fixme: how does this happen?
            if (double.IsNaN(clientStats.SPM) || double.IsNaN(clientStats.Skill) || double.IsInfinity(clientStats.Skill))
            {
                _log.LogWarning("clientStats SPM/Skill NaN {@killInfo}",
                    new
                    {
                        killSPM = killSpm, KDRWeight, totalPlayTime, SPMAgainstPlayWeight, clientStats, scoreDifference
                    });
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
            using var ctx = contextFactory.CreateContext(enableTracking: false);
            var serverStatsSet = ctx.Set<EFServerStatistics>();
            var serverStats = serverStatsSet.FirstOrDefault(s => s.ServerId == serverId);

            if (serverStats != null)
            {
                return serverStats;
            }

            _log.LogDebug("Initializing server stats for {serverId}", serverId);
            // server stats have never been generated before
            serverStats = new EFServerStatistics
            {
                ServerId = serverId,
                TotalKills = 0,
                TotalPlayTime = 0,
            };

            serverStats = serverStatsSet.Add(serverStats).Entity;
            ctx.SaveChanges();

            return serverStats;
        }

        public void ResetKillstreaks(IGameServer gameServer)
        {
            foreach (var session in gameServer.ConnectedClients
                         .Select(client => new
                         {
                             stat = client.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY),
                             detection = client.GetAdditionalProperty<Detection>(CLIENT_DETECTIONS_KEY)
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

        public async Task AddMessageAsync(int clientId, long serverId, bool sentIngame, string message,
            CancellationToken cancellationToken)
        {
            // the web users can have no account
            if (clientId < 1)
            {
                return;
            }

            await using var context = contextFactory.CreateContext(enableTracking: false);
            context.Set<EFClientMessage>().Add(new EFClientMessage()
            {
                ClientId = clientId,
                Message = message,
                ServerId = serverId,
                TimeSent = DateTime.UtcNow,
                SentIngame = sentIngame
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task Sync(IGameServer gameServer, CancellationToken token)
        {
            var serverId = gameServer.LegacyDatabaseId;
            var waiter = _servers[serverId].OnSaving;
            try
            {
                await waiter.WaitAsync(token);

                await using var context = contextFactory.CreateContext();
                var serverStatsSet = context.Set<EFServerStatistics>();
                serverStatsSet.Update(_servers[serverId].ServerStatistics);
                await context.SaveChangesAsync(token);

                foreach (var stats in gameServer.ConnectedClients
                             .Select(client => client.GetAdditionalProperty<EFClientStatistics>(CLIENT_STATS_KEY))
                             .Where(stats => stats != null))
                {
                    await SaveClientStats(stats);
                }

                await SaveHitCache(serverId);
            }

            catch (Exception ex)
            {
                _log.LogError(ex, "There was a problem syncing server stats");
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
    }
}
