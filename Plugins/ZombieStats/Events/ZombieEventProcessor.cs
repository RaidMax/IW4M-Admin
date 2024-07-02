using System.Globalization;
using Data.Models;
using Data.Models.Client;
using Data.Models.Client.Stats;
using Data.Models.Zombie;
using IW4MAdmin.Plugins.ZombieStats.States;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Game.GameScript;
using SharedLibraryCore.Events.Game.GameScript.Zombie;

namespace IW4MAdmin.Plugins.ZombieStats.Events;

public class ZombieEventProcessor(ILogger<ZombieEventProcessor> logger, ZombieClientStateManager stateManager)
{
    private const int RoundsConsidered = 200;

    public void ProcessEvent(GameEventV2 gameEvent)
    {
        if (gameEvent.Origin is not null)
        {
            gameEvent.Origin.GameName = (Reference.Game)gameEvent.Owner.GameName;
        }

        if (gameEvent.Target is not null)
        {
            gameEvent.Target.GameName = (Reference.Game)gameEvent.Owner.GameName;
        }

        switch (gameEvent.GetType().Name)
        {
            case nameof(PlayerKilledGameEvent):
                OnPlayerKilled((PlayerKilledGameEvent)gameEvent);
                break;
            case nameof(PlayerDamageGameEvent):
                OnPlayerDamaged((PlayerDamageGameEvent)gameEvent);
                break;
            case nameof(ZombieKilledGameEvent):
                OnZombieKilled((ZombieKilledGameEvent)gameEvent);
                break;
            case nameof(ZombieDamageGameEvent):
                OnZombieDamaged((ZombieDamageGameEvent)gameEvent);
                break;
            case nameof(PlayerConsumedPerkGameEvent):
                OnPlayerConsumedPerk((PlayerConsumedPerkGameEvent)gameEvent);
                break;
            case nameof(PlayerGrabbedPowerupGameEvent):
                OnPlayerGrabbedPowerup((PlayerGrabbedPowerupGameEvent)gameEvent);
                break;
            case nameof(PlayerRevivedGameEvent):
                OnPlayerRevived((PlayerRevivedGameEvent)gameEvent);
                break;
            case nameof(PlayerDownedGameEvent):
                OnPlayerDowned((PlayerDownedGameEvent)gameEvent);
                break;
            case nameof(PlayerRoundDataGameEvent):
                OnPlayerRoundDataReceived((PlayerRoundDataGameEvent)gameEvent);
                break;
            case nameof(RoundEndEvent):
                OnRoundCompleted((RoundEndEvent)gameEvent);
                break;
            case nameof(PlayerStatUpdatedGameEvent):
                OnPlayerStatUpdated((PlayerStatUpdatedGameEvent)gameEvent);
                break;
        }
    }
    
    public Func<EFClient, EFClientStatistics, double> SkillCalculation()
    {
        return (client, clientStats) =>
        {
            var state = stateManager.GetStateForClient(client);

            if (state is null)
            {
                return clientStats.Skill;
            }

            if (!state.PersistentLifetimeAggregateStats.TryGetValue(client.NetworkId, out var aggregateStats))
            {
                return clientStats.Skill;
            }

            var currentClientRound = state.RoundStates[client.NetworkId];
            var normalizedValues = new List<double>();

            foreach (var key in ZombieAggregateClientStat.RecordsKeys)
            {
                var clientValue =
                    Convert.ToDouble(aggregateStats.GetType().GetProperty(key)!.GetValue(aggregateStats)?.ToString(),
                        CultureInfo.InvariantCulture);
                var maxRecord = stateManager.GetClientNumericalRecord(key) ??
                                stateManager.CreateClientNumericalRecord(
                                    client, currentClientRound.PersistentClientRound, key, clientValue);

                var maxValue = Convert.ToDouble(maxRecord.Value, CultureInfo.InvariantCulture);

                if (clientValue > maxValue)
                {
                    maxRecord.Value = clientValue.ToString(CultureInfo.InvariantCulture);
                    maxRecord.Client = client;
                    maxRecord.Round = currentClientRound.PersistentClientRound;
                    stateManager.TrackUpdatedState(maxRecord);
                }

                if (!ZombieAggregateClientStat.SkillKeys.Contains(key))
                {
                    continue;
                }

                var normalizedValue = clientValue / maxValue;

                if (double.IsNaN(normalizedValue))
                {
                    normalizedValue = 1;
                }

                normalizedValues.Add(normalizedValue);
            }

            var avg = normalizedValues.Any() ? normalizedValues.Average() : 0.0;
            avg *= 1000.0;

            var roundWeightFactor = Math.Max(1, aggregateStats.TotalRoundsPlayed) <= RoundsConsidered
                ? 1.0 / Math.Max(1, aggregateStats.TotalRoundsPlayed)
                : 2.0 / (RoundsConsidered + 1);

            var average = CalculateAverage(clientStats.Skill, avg, roundWeightFactor);

            if (double.IsInfinity(average))
            {
                average = 0;
            }
            
            return average;
        };
    }

    private void OnPlayerKilled(PlayerKilledGameEvent gameEvent)
    {
        RunCalculation(gameEvent.Victim, curr =>
        {
            curr.PersistentClientRound.Deaths++;
            curr.PersistentClientRound.DamageReceived += gameEvent.Damage;
            curr.DiedAt = DateTimeOffset.UtcNow;

            stateManager.TrackEventForLog(gameEvent.Server, EventLogType.Died, curr.PersistentClientRound.Client,
                numericalValue: gameEvent.Damage);
        });
    }

    private void OnPlayerDamaged(PlayerDamageGameEvent gameEvent)
    {
        RunCalculation(gameEvent.Victim, curr =>
        {
            curr.PersistentClientRound.DamageReceived += gameEvent.Damage;

            stateManager.TrackEventForLog(gameEvent.Server, EventLogType.DamageTaken, curr.PersistentClientRound.Client,
                numericalValue: gameEvent.Damage);
        });
    }

    private void OnZombieKilled(ZombieKilledGameEvent gameEvent)
    {
        RunCalculation(gameEvent.Attacker, curr =>
        {
            curr.PersistentClientRound.Kills++;
            curr.PersistentClientRound.DamageDealt += gameEvent.Damage;

            if (gameEvent.HitLocation.StartsWith("head") || gameEvent.MeansOfDeath == "MOD_HEADSHOT")
            {
                curr.PersistentClientRound.Headshots++;
                curr.PersistentClientRound.HeadshotKills++; 
            }

            if (gameEvent.MeansOfDeath == "MOD_MELEE")
            {
                curr.PersistentClientRound.Melees++;
            }
            
            curr.Hits++;
        });
    }
    
    private void OnZombieDamaged(ZombieDamageGameEvent gameEvent)
    {
        RunCalculation(gameEvent.Attacker, curr =>
        {
            curr.PersistentClientRound.DamageDealt += gameEvent.Damage;
            
            if (gameEvent.HitLocation == "head" || gameEvent.MeansOfDeath == "MOD_HEADSHOT")
            {
                curr.PersistentClientRound.Headshots++;
            }

            if (gameEvent.MeansOfDeath == "MOD_MELEE")
            {
                curr.PersistentClientRound.Melees++;
            }

            curr.Hits++;
        });
    }

    private void OnPlayerConsumedPerk(PlayerConsumedPerkGameEvent gameEvent)
    {
        RunCalculation(gameEvent.Client, curr =>
        {
            curr.PersistentClientRound.PerksConsumed++;

            stateManager.TrackEventForLog(gameEvent.Server, EventLogType.PerkConsumed, curr.PersistentClientRound.Client,
                textualValue: gameEvent.PerkName);
        });
    }

    private void OnPlayerGrabbedPowerup(PlayerGrabbedPowerupGameEvent gameEvent)
    {
        RunCalculation(gameEvent.Client, curr =>
        {
            curr.PersistentClientRound.PowerupsGrabbed++;
            
            stateManager.TrackEventForLog(gameEvent.Server, EventLogType.PowerupGrabbed, curr.PersistentClientRound.Client,
                textualValue: gameEvent.PowerupName);
        });
    }

    private void OnPlayerRevived(PlayerRevivedGameEvent gameEvent)
    {
        RunCalculation(gameEvent.Reviver, curr =>
        {
            curr.PersistentClientRound.Revives++;

            stateManager.TrackEventForLog(gameEvent.Server, EventLogType.Revived, curr.PersistentClientRound.Client);
            
            //_stateManager.TrackEventForLog(gameEvent, EventLogType.WasRevived, gameEvent.Revived,
            //    gameEvent.Reviver);
        });
    }

    private void OnPlayerDowned(PlayerDownedGameEvent gameEvent)
    {
        RunCalculation(gameEvent.Client, curr =>
        {
            curr.PersistentClientRound.Downs++;

            stateManager.TrackEventForLog(gameEvent.Server, EventLogType.Downed, curr.PersistentClientRound.Client);
        });
    }
    
    private void OnRoundCompleted(RoundEndEvent roundEndEvent)
    {
        stateManager.StartNextRound(roundEndEvent.RoundNumber, roundEndEvent.Owner);
    }
    
    private void OnPlayerStatUpdated(PlayerStatUpdatedGameEvent gameEvent)
    {
        var tagValue = stateManager.GetStatTagValueForClient(gameEvent.Client, gameEvent.StatTag);

        if (tagValue is null)
        {
            logger.LogWarning("[ZM] Cannot update stat value {Key} for {Client} because no entry exists",
                gameEvent.StatTag, gameEvent.Client.ToString());
            return;
        }

        tagValue.StatValue ??= 0;

        switch (gameEvent.UpdateType)
        {
            case PlayerStatUpdatedGameEvent.StatUpdateType.Absolute:
                tagValue.StatValue = gameEvent.StatValue;
                break;
            case PlayerStatUpdatedGameEvent.StatUpdateType.Increment:
                tagValue.StatValue += gameEvent.StatValue;
                break;
            case PlayerStatUpdatedGameEvent.StatUpdateType.Decrement:
                tagValue.StatValue -= gameEvent.StatValue;
                break;
        }

        if (tagValue.ZombieClientStatTagValueId != 0)
        {
            stateManager.TrackUpdatedState(tagValue);
        }
    }

    private void OnPlayerRoundDataReceived(PlayerRoundDataGameEvent gameEvent)
    {
        RunAggregateCalculation(gameEvent.Client, (matchState, roundState, matchStat, lifetimeStat, lifetimeServerStat) =>
        {
            var currentScore = gameEvent.CurrentScore;
            var isForfeit = gameEvent is { CurrentScore: 0, TotalScore: 0 };

            // in T4 on the last round the current score is set to total score, so we need to 
            // undo that
            if (gameEvent.IsGameOver && !isForfeit)
            {
                var previousCumulativePoints = (int)matchStat.PointsEarned;
                var lastRoundPoints = gameEvent.TotalScore - previousCumulativePoints;
                currentScore = lastRoundPoints + (previousCumulativePoints - (int)matchStat.PointsSpent);
            }

            roundState.PersistentClientRound.Points = currentScore;
            roundState.PersistentClientRound.EndTime = DateTimeOffset.UtcNow;
            roundState.PersistentClientRound.Duration =
                roundState.PersistentClientRound.EndTime - roundState.PersistentClientRound.StartTime;
            roundState.PersistentClientRound.TimeAlive =
                roundState.PersistentClientRound.EndTime.Value - roundState.PersistentClientRound.StartTime;

            if (roundState.DiedAt is not null)
            {
                // subtract the time since they died from the total round time
                roundState.PersistentClientRound.TimeAlive =
                    roundState.PersistentClientRound.TimeAlive.Value.Subtract(
                        roundState.PersistentClientRound.EndTime.Value - roundState.DiedAt.Value);
            }

            var earnedPoints = isForfeit ? 0 : gameEvent.TotalScore - matchStat.PointsEarned;
            var spentPoints = isForfeit ? 0 : Math.Abs(currentScore - earnedPoints - (matchStat.PointsEarned - matchStat.PointsSpent));

            roundState.PersistentClientRound.PointsSpent = spentPoints;
            roundState.PersistentClientRound.PointsEarned = earnedPoints;

            // add to the match aggregates
            foreach (var set in new ZombieClientStat[] { matchStat, lifetimeStat, lifetimeServerStat })
            {
                set.Kills += roundState.PersistentClientRound.Kills;
                set.Deaths += roundState.PersistentClientRound.Deaths;
                set.DamageDealt += roundState.PersistentClientRound.DamageDealt;
                set.DamageReceived += roundState.PersistentClientRound.DamageReceived;
                set.Headshots += roundState.PersistentClientRound.Headshots;
                set.HeadshotKills += roundState.PersistentClientRound.HeadshotKills;
                set.Melees += roundState.PersistentClientRound.Melees;
                set.Downs += roundState.PersistentClientRound.Downs;
                set.Revives += roundState.PersistentClientRound.Revives;
                set.PointsEarned += roundState.PersistentClientRound.PointsEarned;
                set.PointsSpent += roundState.PersistentClientRound.PointsSpent;
                set.PerksConsumed += roundState.PersistentClientRound.PerksConsumed;
                set.PowerupsGrabbed += roundState.PersistentClientRound.PowerupsGrabbed;
            }

            #region maximums and averages

            if (gameEvent.IsGameOver)
            {
                // make it easier to track how many players made it to the end
                matchState.PersistentMatch.ClientsCompleted++;
                lifetimeStat.TotalMatchesCompleted++;
                lifetimeServerStat.TotalMatchesCompleted++;
            }

            CalculateAveragesAndTotals(matchState, lifetimeStat, roundState, matchStat);
            CalculateAveragesAndTotals(matchState, lifetimeServerStat, roundState, matchStat);

            stateManager.TrackEventForLog(gameEvent.Server, EventLogType.RoundCompleted, matchStat.Client,
                numericalValue: gameEvent.CurrentRound);

            #endregion
        });
    }

    private static void CalculateAveragesAndTotals(MatchState matchState,
        ZombieAggregateClientStat lifetimeStat, RoundState roundState, ZombieMatchClientStat matchStat)
    {
        var currentRoundNumber = roundState.PersistentClientRound.RoundNumber;
        // don't credit if played less than 50%
        var shouldCountHighestRound = matchState.RoundNumber > lifetimeStat.HighestRound &&
                                      matchStat.JoinedRound is not null &&
                                      currentRoundNumber - matchStat.JoinedRound.Value >=
                                      currentRoundNumber / 2.0;
        
        if (shouldCountHighestRound)
        {
            lifetimeStat.HighestRound = matchState.RoundNumber;
        }
        
        lifetimeStat.TotalRoundsPlayed++;

        var roundWeightFactor = lifetimeStat.TotalRoundsPlayed <= RoundsConsidered
            ? 1.0 / lifetimeStat.TotalRoundsPlayed
            : 2.0 / (RoundsConsidered + 1);

        var roundKpd = roundState.PersistentClientRound.Kills / Math.Max(1,
            roundState.PersistentClientRound.Deaths + roundState.PersistentClientRound.Downs);
        lifetimeStat.AverageKillsPerDown =
            CalculateAverage(lifetimeStat.AverageKillsPerDown, roundKpd, roundWeightFactor);
        lifetimeStat.AverageDowns =
            CalculateAverage(lifetimeStat.AverageDowns, matchStat.Downs, roundWeightFactor);
        lifetimeStat.AverageRevives =
            CalculateAverage(lifetimeStat.AverageRevives, matchStat.Revives, roundWeightFactor);
        lifetimeStat.AverageRoundReached = CalculateAverage(lifetimeStat.AverageRoundReached,
            matchState.RoundNumber, roundWeightFactor);
        lifetimeStat.AverageMelees =
            CalculateAverage(lifetimeStat.AverageMelees, matchStat.Melees, roundWeightFactor);

        var hsp = roundState.PersistentClientRound.Headshots / (double)Math.Max(1, roundState.Hits);
        lifetimeStat.HeadshotPercentage = CalculateAverage(lifetimeStat.HeadshotPercentage, hsp, roundWeightFactor);

        lifetimeStat.AlivePercentage = CalculateAverage(lifetimeStat.AlivePercentage,
            roundState.PersistentClientRound.TimeAlive!.Value.TotalMilliseconds /
            roundState.PersistentClientRound.Duration!.Value.TotalMilliseconds, roundWeightFactor);

        lifetimeStat.AveragePoints = CalculateAverage(lifetimeStat.AveragePoints,
            roundState.PersistentClientRound.PointsEarned, roundWeightFactor);
    }

    private void RunCalculation(EFClient client, Action<RoundState> calculation)
    {
        if (stateManager.GetStateForClient(client) is not { } match)
        {
            logger.LogWarning("[ZM] No active zombie match for {Client}", client.ToString());
            return;
        }

        if (!match.RoundStates.TryGetValue(client.NetworkId, out var roundState))
        {
            logger.LogWarning("[ZM] No active zombie round for {Client}", client.ToString());
            return;
        }

        calculation(roundState);

        stateManager.TrackUpdatedState(roundState.PersistentClientRound);
    }

    private void RunAggregateCalculation(EFClient client,
        Action<MatchState, RoundState, ZombieMatchClientStat, ZombieAggregateClientStat, ZombieAggregateClientStat> action)
    {
        if (stateManager.GetStateForClient(client) is not { } match)
        {
            logger.LogWarning("[ZM] No active zombie match for {Client}", client.ToString());
            return;
        }

        var currentRoundState = match.RoundStates[client.NetworkId];
        var matchStat = match.PersistentMatchAggregateStats[client.NetworkId];
        var lifetimeAggregateStat = match.PersistentLifetimeAggregateStats[client.NetworkId];
        var lifetimeServerAggregateState = match.PersistentLifetimeServerAggregateStats[client.NetworkId];

        action(match, currentRoundState, matchStat, lifetimeAggregateStat, lifetimeServerAggregateState);

        stateManager.TrackUpdatedState(currentRoundState.PersistentClientRound);
        stateManager.TrackUpdatedState(matchStat);
        stateManager.TrackUpdatedState(lifetimeAggregateStat);
        stateManager.TrackUpdatedState(lifetimeServerAggregateState);
    }
    
    private static double CalculateAverage(double previousAverage, double currentValue, double factor) =>
        Math.Round(currentValue * factor + previousAverage * (1 - factor), 2);
}
