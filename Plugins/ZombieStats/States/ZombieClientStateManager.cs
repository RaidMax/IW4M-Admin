using System.Globalization;
using Data.Abstractions;
using Data.Models;
using Data.Models.Client;
using Data.Models.Client.Stats;
using Data.Models.Zombie;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Plugins.ZombieStats.States;

public class ZombieClientStateManager(
    ILogger<ZombieClientStateManager> logger,
    IDatabaseContextFactory contextFactory,
    ITranslationLookup translations)
{
    private readonly ILogger _logger = logger;
    private readonly Dictionary<(long, Reference.Game), MatchState> _clientMatches = new();
    private readonly List<MatchState> _matches = [];
    private readonly List<MatchState> _recentlyEndedMatches = [];
    private readonly List<DatedRecord> _addedPersistence = [];
    private readonly List<DatedRecord> _updatedPersistence = [];
    private readonly SemaphoreSlim _onWorking = new(1, 1);
    private Dictionary<string, List<ZombieClientStatRecord>> _recordsCache = null!;
    private Dictionary<string, EFClientStatTag> _tagCache = null!;

    public async Task Initialize()
    {
        await using var context = contextFactory.CreateContext(false);
        var records = await context.ZombieClientStatRecords.ToListAsync();
        _recordsCache = records.GroupBy(record => record.Name)
            .ToDictionary(selector => selector.First().Name, selector => selector.ToList());

        _tagCache = await context.ClientStatTags.ToDictionaryAsync(kvp => kvp.TagName, kvp => kvp);
    }

    public async Task UpdateState(CancellationToken token)
    {
        lock (_addedPersistence)
        lock (_updatedPersistence)
        {
            if (!_addedPersistence.Any() && !_updatedPersistence.Any())
            {
                return;
            }

            _logger.LogDebug("[ZM] Updating persistent state for {Count} entries", _addedPersistence.Count);
        }

        await _onWorking.WaitAsync(token);
        await using var context = contextFactory.CreateContext(false);

        try
        {
            IOrderedEnumerable<DatedRecord> addedItems;

            lock (_addedPersistence)
            {
                addedItems = _addedPersistence.ToList().Where(pers => pers.Id == 0).Distinct()
                    .OrderBy(pers => pers.CreatedDateTime);
            }

            foreach (var entity in addedItems)
            {
                try
                {
                    var entry = context.Attach(entity);
                    entry.State = EntityState.Added;
                    await context.SaveChangesAsync(token);
                }
                catch (InvalidOperationException)
                {
                    // ignored
                }
                finally
                {
                    lock (_addedPersistence)
                    {
                        _addedPersistence.Remove(entity);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not persist new object");
        }

        try
        {
            IEnumerable<DatedRecord> updatedItems;

            lock (_updatedPersistence)
            {
                updatedItems = _updatedPersistence.ToList().Distinct();
            }

            foreach (var entity in updatedItems)
            {
                try
                {
                    var entry = context.Attach(entity);
                    entry.Entity.UpdatedDateTime = DateTimeOffset.UtcNow;
                    entry.State = EntityState.Modified;
                    await context.SaveChangesAsync(token);
                }
                catch (InvalidOperationException)
                {
                    // ignored
                }
                finally
                {
                    lock (_updatedPersistence)
                    {
                        _updatedPersistence.Remove(entity);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not persist updates to object");
        }

        _onWorking.Release(1);
    }

    public MatchState? GetStateForClient(EFClient client) =>
        !_clientMatches.TryGetValue((client.NetworkId, client.GameName), out var matchState) ? null : matchState;

    public EFClientStatTagValue? GetStatTagValueForClient(EFClient client, string tagName)
    {
        if (!_tagCache.TryGetValue(tagName, out var statTag))
        {
            _logger.LogDebug("[ZM] Adding new stat tag {Name}", tagName);

            statTag = new EFClientStatTag
            {
                TagName = tagName
            };

            _tagCache[tagName] = statTag;

            TrackNewState(statTag);
        }

        var existingState = GetStateForClient(client);

        if (existingState is null)
        {
            return null;
        }

        if (existingState.PersistentStatTagValues[client.NetworkId].TryGetValue(tagName, out var tagValue))
        {
            return tagValue;
        }

        _logger.LogDebug("[ZM] Adding new stat tag value {Name} for {Client}", tagName, client.ToString());

        tagValue = new EFClientStatTagValue
        {
            ClientId = existingState.PersistentMatchAggregateStats[client.NetworkId].ClientId,
            StatTag = statTag
        };

        existingState.PersistentStatTagValues[client.NetworkId][tagName] = tagValue;

        TrackNewState(tagValue);

        return tagValue;
    }

    public async Task TrackClient(EFClient client, IGameServer server)
    {
        await _onWorking.WaitAsync();

        try
        {
            // check if there is an active match for the server
            if (_matches.FirstOrDefault(match => match.PersistentMatch.ServerId == server.LegacyDatabaseId) is not
                { } serverMatch)
            {
                _logger.LogWarning("[ZM] Could not find active match for game server {Server}", server.Id);
                serverMatch = CreateMatch(server);
            }

            _logger.LogDebug("[ZM] Adding {Client} to existing match", client.ToString());
            await AddPlayerToMatch(client, serverMatch);

            if (_clientMatches.ContainsKey((client.NetworkId, client.GameName)))
            {
                _clientMatches[(client.NetworkId, client.GameName)] = serverMatch;
            }
            else
            {
                _clientMatches.Add((client.NetworkId, client.GameName), serverMatch);
            }
        }
        finally
        {
            _onWorking.Release(1);
        }
    }

    public void UntrackClient(EFClient client, IGameServer server)
    {
        if (_matches.FirstOrDefault(m => m.Server.LegacyDatabaseId == server.LegacyDatabaseId) is not { } match)
        {
            return;
        }

        _logger.LogDebug("[ZM] Removing {Client} from ZombieStateManager tracking", client.ToString());

        _onWorking.Wait();

        try
        {
            match.RoundStates.Remove(client.NetworkId);
            match.PersistentLifetimeAggregateStats.Remove(client.NetworkId);
            match.PersistentLifetimeServerAggregateStats.Remove(client.NetworkId);
            match.PersistentMatchAggregateStats.Remove(client.NetworkId);
            match.PersistentStatTagValues.Remove(client.NetworkId);

            _clientMatches.Remove((client.NetworkId, client.GameName));
        }
        finally
        {
            _onWorking.Release(1);
        }
    }

    private async Task AddPlayerToMatch(EFClient client, MatchState matchState)
    {
        _logger.LogDebug("[ZM] Adding {Client} to zombie match", client.ToString());

        await using var context = contextFactory.CreateContext(false);
        var existingAggregates =
            await context.ZombieClientStatAggregates.Where(aggr => aggr.ClientId == client.ClientId)
                .ToListAsync();

        // ReSharper disable once EntityFramework.NPlusOne.IncompleteDataQuery
        var matchStat = await context.ZombieMatchClientStats
            .Where(match => match.Client.NetworkId == client.NetworkId)
            .Where(match => match.MatchId == matchState.PersistentMatch.ZombieMatchId)
            .FirstOrDefaultAsync();

        var hasConnectedToMatchPreviously = matchStat is not null;

        if (matchStat is null &&
            matchState.PersistentMatchAggregateStats.TryGetValue(client.NetworkId, out var existingMatchStat))
        {
            matchStat = existingMatchStat;
        }

        var isFirstMatchConnection = matchStat is null;

        if (isFirstMatchConnection)
        {
            matchStat = new ZombieMatchClientStat
            {
                Client = client,
                Match = matchState.PersistentMatch
            };

            TrackNewState(matchStat);
        }
        else
        {
            matchStat!.Match = matchState.PersistentMatch;
            _logger.LogDebug("[ZM] Connecting client {Client} has existing data for this match {RoundNumber}",
                client.ToString(), matchState.RoundNumber);
        }

        if (!matchState.RoundStates.ContainsKey(client.NetworkId))
        {
            var roundStat = await context.ZombieRoundClientStats
                .Where(round => round.Client.NetworkId == client.NetworkId)
                .Where(round => round.MatchId == matchState.PersistentMatch.ZombieMatchId)
                .Where(round => round.RoundNumber == matchState.RoundNumber)
                .FirstOrDefaultAsync();

            var roundState = new RoundState
            {
                PersistentClientRound = roundStat ?? new ZombieRoundClientStat
                {
                    Client = client,
                    Match = matchState.PersistentMatch,
                    RoundNumber = 1
                }
            };

            if (roundStat is null)
            {
                TrackNewState(roundState.PersistentClientRound);
            }
            else
            {
                _logger.LogDebug("[ZM] Connecting client {Client} has existing data for this round {RoundNumber}",
                    client.ToString(), matchState.RoundNumber);
            }

            matchState.RoundStates[client.NetworkId] = roundState;
        }

        var existingLifetimeAggregate = existingAggregates.FirstOrDefault(aggregate => aggregate.ServerId is null);
        var existingLifetimeServerAggregate = existingAggregates.FirstOrDefault(aggregate =>
            aggregate.ServerId != null && aggregate.ServerId == matchStat.Match.ServerId);

        if (existingLifetimeAggregate is null)
        {
            existingLifetimeAggregate = new ZombieAggregateClientStat
            {
                Client = client,
                TotalMatchesPlayed = 1
            };

            TrackNewState(existingLifetimeAggregate);
        }
        else if (!hasConnectedToMatchPreviously)
        {
            existingLifetimeAggregate.TotalMatchesPlayed++;
        }

        if (existingLifetimeServerAggregate is null)
        {
            existingLifetimeServerAggregate = new ZombieAggregateClientStat
            {
                Client = client,
                ServerId = matchStat.Match.ServerId,
                TotalMatchesPlayed = 1
            };

            TrackNewState(existingLifetimeServerAggregate);
        }
        else if (!hasConnectedToMatchPreviously)
        {
            existingLifetimeServerAggregate.TotalMatchesPlayed++;
        }

        var statValues = await context.ClientStatTagValues
            .Where(stat => stat.ClientId == client.ClientId)
            .ToDictionaryAsync(selector => selector.StatTag.TagName, selector => selector);

        matchState.PersistentMatchAggregateStats[client.NetworkId] = matchStat;
        matchState.PersistentLifetimeAggregateStats[client.NetworkId] = existingLifetimeAggregate;
        matchState.PersistentLifetimeServerAggregateStats[client.NetworkId] = existingLifetimeServerAggregate;
        matchState.PersistentStatTagValues[client.NetworkId] = statValues;
    }

    private void CarryOverPlayerToMatch(EFClient client, MatchState matchState)
    {
        _logger.LogDebug("[ZM] Client is carrying over from last match {Client}", client.ToString());

        matchState.PersistentMatchAggregateStats[client.NetworkId] = new ZombieMatchClientStat
        {
            Client = client,
            Match = matchState.PersistentMatch
        };

        TrackNewState(matchState.PersistentMatchAggregateStats[client.NetworkId]);

        if (matchState.PersistentLifetimeAggregateStats.TryGetValue(client.NetworkId, out var lifetimeStats))
        {
            lifetimeStats.TotalMatchesPlayed++;
        }

        if (matchState.PersistentLifetimeServerAggregateStats.TryGetValue(client.NetworkId,
                out var lifetimeServerStats))
        {
            lifetimeServerStats.TotalMatchesPlayed++;
        }
    }

    public MatchState CreateMatch(IGameServer server)
    {
        if (_matches.FirstOrDefault(match => match.Server.Id == server.Id) is { } currentMatch)
        {
            _logger.LogWarning("[ZM] Cannot create a new zombie match. One already in progress");
            return currentMatch;
        }

        _logger.LogDebug("[ZM] Creating zombie match for {Server}", server.Id);

        var currentMap = contextFactory.CreateContext(false).Maps.FirstOrDefault(map => map.Name == server.Map.Name);

        var matchPersistence = new ZombieMatch
        {
            ServerId = server.LegacyDatabaseId,
            MatchStartDate = DateTimeOffset.UtcNow,
            MapId = currentMap?.MapId
        };
        var newMatch = new MatchState(server, matchPersistence);
        _matches.Add(newMatch);

        if (_recentlyEndedMatches.FirstOrDefault(match => match.Server.Id == server.Id) is { } previousMatch)
        {
            foreach (var entry in previousMatch.PersistentLifetimeAggregateStats)
            {
                newMatch.PersistentLifetimeAggregateStats.Add(entry.Key, entry.Value);
            }

            foreach (var entry in previousMatch.PersistentLifetimeServerAggregateStats)
            {
                newMatch.PersistentLifetimeServerAggregateStats.Add(entry.Key, entry.Value);
            }

            foreach (var entry in previousMatch.PersistentStatTagValues)
            {
                newMatch.PersistentStatTagValues[entry.Key] = entry.Value;
            }

            _recentlyEndedMatches.Remove(previousMatch);
        }

        foreach (var client in server.ConnectedClients.Where(client =>
                     client.State == SharedLibraryCore.Database.Models.EFClient.ClientState.Connected))
        {
            _logger.LogDebug("[ZM] Adding connected {Client} to new zombie match", client);
            _clientMatches[(client.NetworkId, client.GameName)] = newMatch;
            CarryOverPlayerToMatch(client, newMatch);
        }

        TrackNewState(matchPersistence);
        StartNextRound(1, server);

        return newMatch;
    }

    public void TrackUpdatedState(DatedRecord state)
    {
        lock (_addedPersistence)
        lock (_updatedPersistence)
        {
            if (!_updatedPersistence.Contains(state) && !_addedPersistence.Contains(state))
            {
                _updatedPersistence.Add(state);
            }
        }
    }

    private void TrackNewState(DatedRecord state)
    {
        lock (_addedPersistence)
        lock (_updatedPersistence)
        {
            if (!_addedPersistence.Contains(state) && !_updatedPersistence.Contains(state))
            {
                _addedPersistence.Add(state);
            }
        }
    }

    public void StartNextRound(int round, IGameServer server)
    {
        if (_matches.FirstOrDefault(match => match.Server.Id == server.Id) is not { } matchState)
        {
            _logger.LogWarning("[ZM] Cannot start next round, no active match for {Server}", server.Id);
            return;
        }

        // make sure it's a greater round than previous
        if (matchState.RoundNumber >= round)
        {
            return;
        }

        //_onWorking.Wait();

        _logger.LogDebug("[ZM] Starting Round {RoundNumber} for {Server}", round, server.Id);

        matchState.RoundNumber = round;

        var clients = server.ConnectedClients;
        matchState.RoundStates.Clear();

        foreach (var client in clients.Where(c =>
                     _clientMatches.ContainsKey((c.NetworkId, c.GameName)) &&
                     c.State == SharedLibraryCore.Database.Models.EFClient.ClientState.Connected))
        {
            _logger.LogDebug("[ZM] Updating current round {Client}", client.ToString());

            if (matchState.RoundStates.TryGetValue(client.NetworkId, out var existingRoundState) &&
                existingRoundState.PersistentClientRound.RoundNumber == round)
            {
                _logger.LogDebug("[ZM] Round state data already exists for Client {Client}", client);
                continue;
            }

            var roundState = new RoundState
            {
                PersistentClientRound = new ZombieRoundClientStat
                {
                    Client = client,
                    Match = matchState.PersistentMatch,
                    RoundNumber = round
                }
            };

            if (matchState.PersistentMatchAggregateStats.TryGetValue(client.NetworkId, out var matchStats))
            {
                matchStats.JoinedRound ??= round;
            }

            TrackNewState(roundState.PersistentClientRound);
            matchState.RoundStates[client.NetworkId] = roundState;
        }

        //_onWorking.Release(1);
    }

    public void EndMatch(IGameServer gameServer)
    {
        if (_matches.FirstOrDefault(match => match.Server.Id == gameServer.Id) is not { } matchState)
        {
            _logger.LogWarning("[ZM] Cannot end zombie match. Server has not started a match");
            return;
        }

        _onWorking.Wait();

        try
        {
            _logger.LogDebug("[ZM] Ending match for zombie match on {Server}", matchState.Server.Id);

            matchState.PersistentMatch.MatchEndDate = DateTimeOffset.UtcNow;

            _matches.Remove(matchState);
            _recentlyEndedMatches.Add(matchState);

            foreach (var kvp in _clientMatches.ToList().Where(kvp => kvp.Value == matchState))
            {
                _clientMatches.Remove(kvp.Key);
            }

            TrackUpdatedState(matchState.PersistentMatch);
        }
        finally
        {
            _onWorking.Release(1);
        }
    }

    public ZombieClientStatRecord? GetClientNumericalRecord(string recordKey, RecordType type = RecordType.Maximum)
    {
        return _recordsCache.TryGetValue(recordKey, out var values)
            ? values.FirstOrDefault(record => record.Type == type.ToString())
            : null;
    }

    public ZombieClientStatRecord CreateClientNumericalRecord(EFClient client, ZombieRoundClientStat round,
        string recordKey,
        double recordValue)
    {
        _onWorking.Wait();

        try
        {
            var newRecord = new ZombieClientStatRecord
            {
                Name = recordKey,
                Value = Convert.ToString(recordValue, CultureInfo.InvariantCulture),
                Type = RecordType.Maximum.ToString(),
                Client = client,
                Round = round
            };

            if (_recordsCache.TryGetValue(recordKey, out var values))
            {
                values.Add(newRecord);
            }
            else
            {
                _recordsCache.Add(recordKey, [newRecord]);
            }

            TrackNewState(newRecord);

            return newRecord;
        }
        finally
        {
            _onWorking.Release(1);
        }
    }

    public async Task GetTopStatsMetrics(Dictionary<int, List<EFMeta>> meta, long? serverId, string performanceBucket,
        bool isTopStats)
    {
        if (!isTopStats)
        {
            return;
        }

        var clientIds = meta.Keys.ToList();

        await using var context = contextFactory.CreateContext(false);
        var stats = await context.ZombieClientStatAggregates
            .Where(stat => clientIds.Contains(stat.ClientId))
            .Where(stat => stat.ServerId == serverId)
            .ToListAsync();

        foreach (var stat in stats)
        {
            meta[stat.ClientId].Add(new EFMeta
            {
                Value = stat.HighestRound.ToNumericalString(),
                Key = "Highest Round"
            });
            meta[stat.ClientId].Add(new EFMeta
            {
                Value = stat.TotalRoundsPlayed.ToNumericalString(),
                Key = "Total Rounds Played"
            });
            meta[stat.ClientId].First(m => m.Extra == "Deaths").Value = stat.Deaths.ToNumericalString();
        }
    }

    public async Task GetAdvancedStatsMetrics(Dictionary<int, List<EFMeta>> meta, long? serverId,
        string performanceBucketCode,
        bool isTopStats)
    {
        if (isTopStats || !meta.Any())
        {
            return;
        }

        var clientId = meta.First().Key;

        await using var context = contextFactory.CreateContext(false);
        var iqStats = context.ZombieClientStatAggregates
            .Where(stat => stat.ClientId == clientId);

        iqStats = !string.IsNullOrEmpty(performanceBucketCode)
            ? iqStats.Where(stat => stat.Server.PerformanceBucket.Code == performanceBucketCode)
            : iqStats.Where(stat => stat.ServerId == serverId);

        var stats = await iqStats.Select(stat => new
            {
                stat.HeadshotKills,
                stat.DamageDealt,
                stat.DamageReceived,
                stat.Downs,
                stat.Revives,
                stat.PointsEarned,
                stat.PointsSpent,
                stat.PerksConsumed,
                stat.PowerupsGrabbed,
                stat.HighestRound,
                stat.TotalRoundsPlayed,
                stat.TotalMatchesPlayed,
                stat.TotalMatchesCompleted,
                stat.HeadshotPercentage,
                stat.AverageRoundReached,
                stat.AveragePoints,
                stat.AverageDowns,
                stat.AverageRevives
            })
            .FirstOrDefaultAsync();

        if (stats is null)
        {
            return;
        }

        var tagValues = await context.ClientStatTagValues
            .Where(tag => tag.ClientId == clientId)
            .Select(tag => new
            {
                tag.StatValue,
                tag.StatTag.TagName
            })
            .ToListAsync();

        meta.First().Value.AddRange(new List<EFMeta>
        {
            new()
            {
                Key = "Headshot Kills",
                Value = stats.HeadshotKills.ToNumericalString()
            },
            new()
            {
                Key = "Damage Dealt",
                Value = stats.DamageDealt.ToNumericalString()
            },
            new()
            {
                Key = "Damage Received",
                Value = stats.DamageReceived.ToNumericalString()
            },
            new()
            {
                Key = "Downs",
                Value = stats.Downs.ToNumericalString()
            },
            new()
            {
                Key = "Revives",
                Value = stats.Revives.ToNumericalString()
            },
            new()
            {
                Key = "Points Earned",
                Value = stats.PointsEarned.ToNumericalString()
            },
            new()
            {
                Key = "Points Spent",
                Value = stats.PointsSpent.ToNumericalString()
            },
            new()
            {
                Key = "Perks Consumed",
                Value = stats.PerksConsumed.ToNumericalString()
            },
            new()
            {
                Key = "Powerups Grabbed",
                Value = stats.PowerupsGrabbed.ToNumericalString()
            },
            new()
            {
                Key = "Highest Round",
                Value = stats.HighestRound.ToNumericalString()
            },
            new()
            {
                Key = "Rounds Played",
                Value = stats.TotalRoundsPlayed.ToNumericalString()
            },
            new()
            {
                Key = "Matches Played",
                Value = stats.TotalMatchesPlayed.ToNumericalString()
            },
            new()
            {
                Key = "Matches Completed",
                Value = stats.TotalMatchesCompleted.ToNumericalString()
            },
            new()
            {
                Key = "Quit Rate",
                Value = (stats.TotalMatchesCompleted == 0
                        ? 100
                        : stats.TotalMatchesCompleted - stats.TotalMatchesPlayed == 0
                            ? 0
                            : (int)Math.Round((1 - stats.TotalMatchesCompleted / (double)stats.TotalMatchesPlayed) *
                                              100.0))
                    .ToNumericalString() + "%"
            },
            new()
            {
                Key = "Headshot Percentage",
                Value = (stats.HeadshotPercentage * 100.0).ToNumericalString() + "%"
            },
            new()
            {
                Key = "Avg. Round Reached",
                Value = stats.AverageRoundReached.ToNumericalString(1)
            },
            new()
            {
                Key = "Avg. Points",
                Value = stats.AveragePoints.ToNumericalString()
            },
            new()
            {
                Key = "Avg. Downs",
                Value = stats.AverageDowns.ToNumericalString(2)
            },
            new()
            {
                Key = "Avg. Revives",
                Value = stats.AverageRevives.ToNumericalString(2)
            }
        });
        meta.First().Value.AddRange(tagValues.Select(tag => new EFMeta
        {
            Key = translations[$"WEBFRONT_STAT_TAG_{tag.TagName.ToUpper()}"],
            Value = tag.StatValue?.ToNumericalString() ?? "-"
        }));
    }

    public void TrackEventForLog(IGameServer gameServer, EventLogType eventType, EFClient? sourceClient = null,
        EFClient? associatedClient = null, double? numericalValue = null, string? textualValue = null)
    {
        var match = (ZombieMatch?)null;
        var matchedClient = sourceClient;

        if (sourceClient is not null)
        {
            if (_clientMatches.TryGetValue((sourceClient.NetworkId, sourceClient.GameName), out var foundMatch))
            {
                if (foundMatch.PersistentLifetimeAggregateStats.TryGetValue(sourceClient.ClientId, out var foundStat))
                {
                    matchedClient = foundStat.Client;
                }
            }

            match = GetStateForClient(sourceClient)?.PersistentMatch;
        }

        match ??= _matches
            .FirstOrDefault(state => state.PersistentMatch.ServerId == gameServer.LegacyDatabaseId)
            ?.PersistentMatch;

        var eventLogData = new ZombieEventLog
        {
            EventType = eventType,
            SourceClient = matchedClient,
            AssociatedClient = associatedClient,
            NumericalValue = numericalValue,
            TextualValue = textualValue,
            Match = match
        };

        TrackNewState(eventLogData);
    }
}
