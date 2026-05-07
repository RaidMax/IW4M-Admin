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
        {"AD", ParseZombieDamageEvent},
        {"AK", ParseZombieKilledEvent},
        {"RD", ParsePlayerRoundDataEvent},
        {"RC", ParseRoundCompleteEvent},
        {"ZE", ParseZombieEvent},
        {"EE", ParseEasterEggCompleteEvent},
    };

    private const string GsePrefix = "GSE";

    public GameEventV2? ParseScriptEvent(GameScriptEvent scriptEvent)
    {
        var eventArgs = scriptEvent.ScriptData.Split(DataSeparator);

        if (eventArgs.Length < 2)
        {
            logger.LogDebug("Ignoring {EventType} because there is not enough data {Data}", nameof(GameScriptEvent),
                scriptEvent.ScriptData);
            return null;
        }

        // Other subsystems (latency probe, anti-cheat, live radar) emit script events
        // that share the ScriptEventTriggered dispatch but use different wire formats.
        // Silently skip anything not "GSE;<type>;..." — those events have dedicated handlers.
        if (eventArgs[0] != GsePrefix)
        {
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

    #region Combat events (unchanged)

    private static GameEventV2 ParsePlayerKilledEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var (victim, attacker) = ParseClientInfo(scriptEvent, data);

        return new PlayerKilledGameEvent
        {
            Target = victim,
            Origin = attacker,
            WeaponName = data[8],
            Damage = Convert.ToInt32(data[9]),
            MeansOfDeath = data[10],
            HitLocation = data[11]
        };
    }

    private static GameEventV2 ParsePlayerDamageEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var (victim, attacker) = ParseClientInfo(scriptEvent, data);

        return new PlayerDamageGameEvent
        {
            Target = victim,
            Origin = attacker,
            WeaponName = data[8],
            Damage = Convert.ToInt32(data[9]),
            MeansOfDeath = data[10],
            HitLocation = data[11]
        };
    }

    private static GameEventV2 ParseZombieDamageEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var (victim, attacker) = ParseClientInfo(scriptEvent, data);

        return new ZombieDamageGameEvent
        {
            Target = victim,
            Origin = attacker,
            WeaponName = data[8],
            Damage = Convert.ToInt32(data[9]),
            MeansOfDeath = data[10],
            HitLocation = data[11]
        };
    }

    private static GameEventV2 ParseZombieKilledEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var (victim, attacker) = ParseClientInfo(scriptEvent, data);

        return new ZombieKilledGameEvent
        {
            Target = victim,
            Origin = attacker,
            WeaponName = data[8],
            Damage = Convert.ToInt32(data[9]),
            MeansOfDeath = data[10],
            HitLocation = data[11]
        };
    }

    #endregion

    #region Round events (unchanged)

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

    // Two formats share the EE prefix:
    //   GSE;EE;{mapName}        — canonical match-complete (fires once at terminal notify)
    //   GSE;EE;step;{stepKey}   — per-step progress marker (may fire multiple times across a match)
    // First field disambiguates; "step" is reserved.
    private static GameEventV2 ParseEasterEggCompleteEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var first = data.ElementAtOrDefault(0) ?? string.Empty;

        if (string.Equals(first, "step", StringComparison.OrdinalIgnoreCase))
        {
            return new EasterEggStepGameEvent
            {
                StepKey = data.ElementAtOrDefault(1) ?? string.Empty
            };
        }

        return new EasterEggCompleteGameEvent
        {
            MapName = first
        };
    }

    #endregion

    #region Unified ZE parser

    // Format: GSE;ZE;{guid;clientNum;team;name};{category};{action?};{...details}
    // After split and eventArgs[2..], data is: [guid, clientNum, team, name, category, ...]
    private static GameEventV2 ParseZombieEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var category = data[4];

        return category switch
        {
            "down" => ParseZeDown(scriptEvent, data),
            "revive" => ParseZeRevive(scriptEvent, data),
            "perk" => ParseZePerk(scriptEvent, data),
            "powerup" => ParseZePowerup(scriptEvent, data),
            "weapon" => ParseZeWeapon(scriptEvent, data),
            "box" => ParseZeBox(scriptEvent, data),
            "door" => ParseZeDoor(scriptEvent, data),
            "trap" => ParseZeTrap(scriptEvent, data),
            "build" => ParseZeBuild(scriptEvent, data),
            _ => throw new ArgumentException($"Unknown ZE category: {category}")
        };
    }

    // ZE;{player};down
    private static GameEventV2 ParseZeDown(GameScriptEvent scriptEvent, string[] data)
    {
        return new PlayerDownedGameEvent
        {
            Origin = ParseVictimClient(scriptEvent, data)
        };
    }

    // ZE;{revived};revive;{reviver guid;cnum;team;name}
    private static GameEventV2 ParseZeRevive(GameScriptEvent scriptEvent, string[] data)
    {
        var revived = ParseVictimClient(scriptEvent, data);

        var reviverGuid = data[5].ConvertGuidToLong(scriptEvent.Owner.EventParser.Configuration.GuidNumberStyle);
        var reviver = new EFClient
        {
            NetworkId = reviverGuid,
            ClientNumber = Convert.ToInt32(data[6]),
            TeamName = data[7],
            CurrentAlias = new EFAlias { Name = data[8] }
        };

        return new PlayerRevivedGameEvent
        {
            Origin = reviver,
            Target = revived
        };
    }

    // ZE;{player};perk;buy;{perkName};{cost}
    private static GameEventV2 ParseZePerk(GameScriptEvent scriptEvent, string[] data)
    {
        return new PlayerConsumedPerkGameEvent
        {
            Origin = ParseVictimClient(scriptEvent, data),
            PerkName = data[6],
            Cost = data.Length > 7 ? Convert.ToInt32(data[7]) : 0
        };
    }

    // ZE;{player};powerup;grab;{powerupName}
    private static GameEventV2 ParseZePowerup(GameScriptEvent scriptEvent, string[] data)
    {
        return new PlayerGrabbedPowerupGameEvent
        {
            Origin = ParseVictimClient(scriptEvent, data),
            PowerupName = data[6]
        };
    }

    // ZE;{player};weapon;buy;{weaponName};{cost}
    // ZE;{player};weapon;upgrade;{oldWeapon};{newWeapon};{cost}
    // ZE;{player};weapon;abandon;{weaponName};{cost}
    private static GameEventV2 ParseZeWeapon(GameScriptEvent scriptEvent, string[] data)
    {
        var action = data[5];
        var client = ParseVictimClient(scriptEvent, data);

        return action switch
        {
            "buy" => new WeaponPurchaseGameEvent
            {
                Origin = client,
                WeaponName = data[6],
                Cost = Convert.ToInt32(data[7])
            },
            "upgrade" => new PackAPunchGameEvent
            {
                Origin = client,
                Outcome = PackAPunchGameEvent.PaPOutcome.Upgrade,
                OldWeapon = data[6],
                NewWeapon = data[7],
                Cost = Convert.ToInt32(data[8])
            },
            "abandon" => new PackAPunchGameEvent
            {
                Origin = client,
                Outcome = PackAPunchGameEvent.PaPOutcome.Abandon,
                OldWeapon = data[6],
                Cost = Convert.ToInt32(data[7])
            },
            _ => throw new ArgumentException($"Unknown weapon action: {action}")
        };
    }

    // ZE;{player};box;take;{weaponName};{cost}
    // ZE;{player};box;pass;{weaponName};{cost}
    // ZE;{player};box;teddy;{cost}
    private static GameEventV2 ParseZeBox(GameScriptEvent scriptEvent, string[] data)
    {
        var action = data[5];
        var client = ParseVictimClient(scriptEvent, data);

        return action switch
        {
            "take" => new BoxUseGameEvent
            {
                Origin = client,
                Outcome = BoxUseGameEvent.BoxOutcome.Take,
                WeaponName = data[6],
                Cost = Convert.ToInt32(data[7])
            },
            "pass" => new BoxUseGameEvent
            {
                Origin = client,
                Outcome = BoxUseGameEvent.BoxOutcome.Pass,
                WeaponName = data[6],
                Cost = Convert.ToInt32(data[7])
            },
            "teddy" => new BoxUseGameEvent
            {
                Origin = client,
                Outcome = BoxUseGameEvent.BoxOutcome.Teddy,
                Cost = Convert.ToInt32(data[6])
            },
            _ => throw new ArgumentException($"Unknown box action: {action}")
        };
    }

    // ZE;{player};door;buy;{cost}
    private static GameEventV2 ParseZeDoor(GameScriptEvent scriptEvent, string[] data)
    {
        return new DoorPurchaseGameEvent
        {
            Origin = ParseVictimClient(scriptEvent, data),
            Cost = Convert.ToInt32(data[6])
        };
    }

    // ZE;{player};trap;activate;{trapType};{cost}
    private static GameEventV2 ParseZeTrap(GameScriptEvent scriptEvent, string[] data)
    {
        return new TrapActivateGameEvent
        {
            Origin = ParseVictimClient(scriptEvent, data),
            TrapType = data[6],
            Cost = Convert.ToInt32(data[7])
        };
    }

    // ZE;{player};build;complete;{buildableName}
    private static GameEventV2 ParseZeBuild(GameScriptEvent scriptEvent, string[] data)
    {
        return new BuildCompleteGameEvent
        {
            Origin = ParseVictimClient(scriptEvent, data),
            BuildableName = data[6]
        };
    }

    #endregion

    #region Client parsing helpers

    private static (EFClient victim, EFClient attacker) ParseClientInfo(GameScriptEvent scriptEvent, string[] data)
    {
        var victim = ParseVictimClient(scriptEvent, data);

        var attackerGuid = data[4].ConvertGuidToLong(scriptEvent.Owner.EventParser.Configuration.GuidNumberStyle);
        var attacker = new EFClient
        {
            NetworkId = attackerGuid,
            ClientNumber = Convert.ToInt32(data[5]),
            TeamName = data[6],
            CurrentAlias = new EFAlias { Name = data[7] }
        };

        return (victim, attacker);
    }

    private static EFClient ParseVictimClient(GameScriptEvent scriptEvent, string[] data)
    {
        var victimGuid = data[0].ConvertGuidToLong(scriptEvent.Owner.EventParser.Configuration.GuidNumberStyle);

        return new EFClient
        {
            NetworkId = victimGuid,
            ClientNumber = Convert.ToInt32(data[1]),
            TeamName = data[2],
            CurrentAlias = new EFAlias { Name = data[3] }
        };
    }

    #endregion
}
