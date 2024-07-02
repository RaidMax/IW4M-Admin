using System.ComponentModel;
using Data.Models;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Game.GameScript;
using SharedLibraryCore.Events.Game.GameScript.Zombie;

namespace IW4MAdmin.Plugins.ZombieStats.Events;

public class ZombieEventParser(ILogger<ZombieEventParser> logger)
{
    private const char DataSeparator = ';';
    private readonly Dictionary<string, Func<GameScriptEvent, string[], GameEventV2>> _eventParsers = new()
    {
        {"K", ParsePlayerKilledEvent},
        {"D", ParsePlayerDamageEvent},
        {"PD", ParsePlayerDownedEvent},
        {"PR", ParsePlayerRevivedEvent},
        {"PC", ParsePlayerConsumedPerkEvent},
        {"PG", ParsePlayerGrabbedPowerupEvent},
        {"AD", ParseZombieDamageEvent},
        {"AK", ParseZombieKilledEvent},
        {"RD", ParsePlayerRoundDataEvent},
        {"RC", ParseRoundCompleteEvent},
        {"SU", ParsePlayerStatUpdatedEvent},
    };

    public GameEventV2? ParseScriptEvent(GameScriptEvent scriptEvent)
    {
        var eventArgs = scriptEvent.ScriptData.Split(DataSeparator);

        if (eventArgs.Length < 2)
        {
            logger.LogDebug("Ignoring {EventType} because there is not enough data {Data}", nameof(GameScriptEvent),
                scriptEvent.ScriptData);
            return null;
        }

        if (!_eventParsers.TryGetValue(eventArgs[1], out var parser))
        {
            logger.LogWarning("No parser registered for GSE type \"{Type}\"", eventArgs[1]);
            return null;
        }
        
        var parsedEvent = parser(scriptEvent, eventArgs[2..]);

        logger.LogDebug("Parsed GSE type {Type}", parsedEvent.GetType().Name);

        return parsedEvent;
    }

    private static GameEventV2 ParsePlayerKilledEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var (victim, attacker) = ParseClientInfo(scriptEvent, data);

        var killEvent = new PlayerKilledGameEvent
        {
            Target = victim,
            Origin = attacker,
            WeaponName = data[8],
            Damage = Convert.ToInt32(data[9]),
            MeansOfDeath = data[10],
            HitLocation = data[11]
        };

        return killEvent;
    }
    
    private static GameEventV2 ParsePlayerDamageEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var (victim, attacker) = ParseClientInfo(scriptEvent, data);

        var killEvent = new PlayerDamageGameEvent
        {
            Target = victim,
            Origin = attacker,
            WeaponName = data[8],
            Damage = Convert.ToInt32(data[9]),
            MeansOfDeath = data[10],
            HitLocation = data[11]
        };

        return killEvent;
    }

    private static GameEventV2 ParsePlayerRevivedEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var (revived, reviver) = ParseClientInfo(scriptEvent, data);

        return new PlayerRevivedGameEvent
        {
            Origin = reviver,
            Target = revived
        };
    }

    private static GameEventV2 ParsePlayerConsumedPerkEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var consumer = ParseVictimClient(scriptEvent, data);

        return new PlayerConsumedPerkGameEvent
        {
            Origin = consumer,
            PerkName = data.Last()
        };
    }

    private static GameEventV2 ParsePlayerGrabbedPowerupEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var consumer = ParseVictimClient(scriptEvent, data);

        return new PlayerGrabbedPowerupGameEvent
        {
            Origin = consumer,
            PowerupName = data.Last()
        };
    }

    private static GameEventV2 ParseZombieDamageEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var (victim, attacker) = ParseClientInfo(scriptEvent, data);

        var killEvent = new ZombieDamageGameEvent
        {
            Target = victim,
            Origin = attacker,
            WeaponName = data[8],
            Damage = Convert.ToInt32(data[9]),
            MeansOfDeath = data[10],
            HitLocation = data[11]
        };

        return killEvent;
    }
    
    private static GameEventV2 ParseZombieKilledEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var (victim, attacker) = ParseClientInfo(scriptEvent, data);

        var killEvent = new ZombieKilledGameEvent
        {
            Target = victim,
            Origin = attacker,
            WeaponName = data[8],
            Damage = Convert.ToInt32(data[9]),
            MeansOfDeath = data[10],
            HitLocation = data[11]
        };

        return killEvent;
    }

    private static GameEventV2 ParsePlayerDownedEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var client = ParseVictimClient(scriptEvent, data);

        return new PlayerDownedGameEvent
        {
            Origin = client
        };
    }

    private static GameEventV2 ParsePlayerRoundDataEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var client = ParseVictimClient(scriptEvent, data);
        
        return new PlayerRoundDataGameEvent
        {
            Origin = client,
            TotalScore = Convert.ToInt32(data[4]),
            CurrentScore = Convert.ToInt32(data[5]),
            CurrentRound = Convert.ToInt32(data[6]),
            IsGameOver = data[7] == "1"
        };
    }
    
    private static GameEventV2 ParseRoundCompleteEvent(GameScriptEvent scriptEvent, string[] data)
    {
        return new RoundEndEvent
        {
            RoundNumber = Convert.ToInt32(data[0])
        };
    }
    
    private static GameEventV2 ParsePlayerStatUpdatedEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var client = ParseVictimClient(scriptEvent, data);
        var rawStatValue = data[^1];
        var updateType = rawStatValue[0] switch
        {
            '+' => PlayerStatUpdatedGameEvent.StatUpdateType.Increment,
            '-' => PlayerStatUpdatedGameEvent.StatUpdateType.Decrement,
            '=' => PlayerStatUpdatedGameEvent.StatUpdateType.Absolute,
            _ => throw new InvalidEnumArgumentException($"{rawStatValue[0]} is not a valid state update type")
        };
        
        return new PlayerStatUpdatedGameEvent
        {
            Origin = client,
            UpdateType = updateType,
            StatTag = data[^2],
            StatValue = int.Parse(rawStatValue[1..])
        };
    }
    
    private static (EFClient victim, EFClient attacker) ParseClientInfo(GameScriptEvent scriptEvent, string[] data)
    {
        var victim = ParseVictimClient(scriptEvent, data);

        var attackerGuid = data[4].ConvertGuidToLong(scriptEvent.Owner.EventParser.Configuration.GuidNumberStyle);
        var attackerClientNum = Convert.ToInt32(data[5]);
        var attackerTeam = data[6];
        var attackerName = data[7];

        var attacker = new EFClient
        {
            NetworkId = attackerGuid,
            ClientNumber = attackerClientNum,
            TeamName = attackerTeam,
            CurrentAlias = new EFAlias
            {
                Name = attackerName
            }
        };

        return (victim, attacker);
    }

    private static EFClient ParseVictimClient(GameScriptEvent scriptEvent, string[] data)
    {
        var victimGuid = data[0].ConvertGuidToLong(scriptEvent.Owner.EventParser.Configuration.GuidNumberStyle);
        var victimClientNum = Convert.ToInt32(data[1]);
        var victimTeam = data[2];
        var victimName = data[3];

        var victim = new EFClient
        {
            NetworkId = victimGuid,
            ClientNumber = victimClientNum,
            TeamName = victimTeam,
            CurrentAlias = new EFAlias
            {
                Name = victimName
            }
        };
        
        return victim;
    }
}
