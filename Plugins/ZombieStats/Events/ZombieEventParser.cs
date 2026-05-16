using Data.Models;
using Data.Models.Zombie;
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
        {"ZP", ParseZombieEvent},   // player-scoped: ZP;<player>;<category>;...
        {"ZW", ParseWorldEvent},    // world-scoped:  ZW;<kind>;<args...>
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

    // GSE;ZW;<kind>;<args...> — unified prefix for "world-scoped" zombie events that
    // don't have a single owning player. Mirrors the ZP pattern (which is the same
    // idea for player-scoped events). The kind discriminator lets us add new world
    // events without consuming another top-level prefix per event type. Round
    // number is included on each kind that needs correlation against the live
    // round (defends against the log-tail race where ZW arrives just before/after
    // the matching RC and could otherwise mis-attribute).
    //
    // Current kinds:
    //   ZW;round_special;<round>;<type>                   — special-round designation
    //   ZW;zombies;<round>;<remaining>;<alive>            — periodic spawn-state snapshot
    //   ZW;power;<state>;<source>;[player block]          — power-state change
    //   ZW;easter_egg;step;<key>                          — EE step waypoint
    //   ZW;easter_egg;complete;<map>                      — EE canonical completion
    private static GameEventV2 ParseWorldEvent(GameScriptEvent scriptEvent, string[] data)
    {
        var kind = data.Length > 0 ? data[0] : string.Empty;
        var args = data.Length > 1 ? data[1..] : Array.Empty<string>();
        return kind switch
        {
            "round_special" => ParseRoundSpecial(args),
            "zombies"       => ParseZombiesRemaining(args),
            "power"         => ParsePowerStateChange(scriptEvent, args),
            "easter_egg"    => ParseEasterEgg(args),
            _               => throw new ArgumentException($"Unknown ZW kind: {kind}"),
        };
    }

    // ZW;easter_egg;<step|complete>;<value>
    //   step    — args[1] is the canonical step key (e.g. t4_vr_radio_1)
    //   complete — args[1] is the map name (matches level.script)
    // Folded into ZW from the legacy EE prefix; the discriminator-prefix ("step" /
    // "complete") removes the previous "first-arg-could-be-anything" ambiguity.
    private static GameEventV2 ParseEasterEgg(string[] args)
    {
        var subKind = args.Length > 0 ? args[0] : string.Empty;
        if (string.Equals(subKind, "step", StringComparison.OrdinalIgnoreCase))
        {
            return new EasterEggStepGameEvent
            {
                StepKey = args.Length > 1 ? args[1] : string.Empty
            };
        }
        // "complete" or anything else falls back to the canonical complete event
        // (defensive — pre-rename emissions just had the map name as first arg).
        return new EasterEggCompleteGameEvent
        {
            MapName = args.Length > 1 ? args[1] : string.Empty
        };
    }

    // ZW;zombies;<round>;<remaining>;<alive> — periodic engine snapshot for live SPH.
    // Emitted ~every 5s by the GSC WatchZombiesRemaining watcher when either count
    // changes. args: [round, remaining, alive].
    private static GameEventV2 ParseZombiesRemaining(string[] args)
    {
        return new ZombiesRemainingGameEvent
        {
            RoundNumber = Convert.ToInt32(args[0]),
            Remaining = Convert.ToInt32(args[1]),
            Alive = Convert.ToInt32(args[2]),
        };
    }

    // ZW;round_special;<round>;<type> — emitted at round-start for special rounds
    // (dog/monkey/leaper). Lets the premium plugin tag the round and skip
    // Seconds-Per-Horde where the static budget formula doesn't apply. args:
    // [round, type]. Unknown tokens map to null (handler treats as no-op rather
    // than crashing the pipeline).
    private static GameEventV2 ParseRoundSpecial(string[] args)
    {
        var token = args.Length > 1 ? args[1] : string.Empty;
        return new RoundSpecialGameEvent
        {
            RoundNumber = Convert.ToInt32(args[0]),
            SpecialType = ZombieSpecialRoundTypeExtensions.FromGsc(token)
        };
    }

    // ZW;power;<state>;<source>;[guid;cnum;team;name]
    //   state  = on | off
    //   source = world | player
    //   player block present iff source == player
    // No round number — power state spans the whole match, no correlation needed.
    private static GameEventV2 ParsePowerStateChange(GameScriptEvent scriptEvent, string[] data)
    {
        var state = data[0] switch
        {
            "on" => PowerState.On,
            "off" => PowerState.Off,
            _ => throw new ArgumentException($"Unknown PWR state: {data[0]}")
        };

        var source = data[1] switch
        {
            "world" => PowerSource.World,
            "player" => PowerSource.Player,
            _ => throw new ArgumentException($"Unknown PWR source: {data[1]}")
        };

        var evt = new PowerStateChangeGameEvent
        {
            State = state,
            Source = source
        };

        if (source == PowerSource.Player)
        {
            // Player block follows: data[2..5] = guid, cnum, team, name
            // Reuse same field layout as ParseVictimClient but offset shifted.
            var guid = data[2].ConvertGuidToLong(scriptEvent.Owner.EventParser.Configuration.GuidNumberStyle);
            evt.Origin = new EFClient
            {
                NetworkId = guid,
                ClientNumber = Convert.ToInt32(data[3]),
                TeamName = data[4],
                CurrentAlias = new EFAlias { Name = data[5] }
            };
        }

        return evt;
    }

    #endregion

    #region Unified ZP (player) parser

    // Format: GSE;ZP;{guid;clientNum;team;name};{category};{action?};{...details}
    // After split and eventArgs[2..], data is: [guid, clientNum, team, name, category, ...]
    // (Renamed from ZE for symmetry with ZW — both are "Z<scope>" prefixes.)
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
            "gum" => ParseZeGobbleGum(scriptEvent, data),
            "bank" => ParseZeBank(scriptEvent, data),
            "locker" => ParseZeLocker(scriptEvent, data),
            _ => throw new ArgumentException($"Unknown ZE category: {category}")
        };
    }

    // ZE;{player};bank;deposit;{amount}    — T6 Tranzit/Die Rise/Buried
    // ZE;{player};bank;withdraw;{amount}
    private static GameEventV2 ParseZeBank(GameScriptEvent scriptEvent, string[] data)
    {
        var action = data.Length > 5 ? data[5] : string.Empty;
        var amount = data.Length > 6 && int.TryParse(data[6], out var parsed) ? parsed : 0;

        return action switch
        {
            "deposit" => new BankTransactionGameEvent
            {
                Origin = ParseVictimClient(scriptEvent, data),
                IsDeposit = true,
                Amount = amount
            },
            "withdraw" => new BankTransactionGameEvent
            {
                Origin = ParseVictimClient(scriptEvent, data),
                IsDeposit = false,
                Amount = amount
            },
            _ => throw new ArgumentException($"Unknown bank action: {action}")
        };
    }

    // ZE;{player};locker;store;{weapon}      — T6 Tranzit/Die Rise/Buried
    // ZE;{player};locker;retrieve;{weapon}
    private static GameEventV2 ParseZeLocker(GameScriptEvent scriptEvent, string[] data)
    {
        var action = data.Length > 5 ? data[5] : string.Empty;
        var weaponName = data.Length > 6 ? data[6] : string.Empty;

        return action switch
        {
            "store" => new WeaponLockerGameEvent
            {
                Origin = ParseVictimClient(scriptEvent, data),
                IsStore = true,
                WeaponName = weaponName
            },
            "retrieve" => new WeaponLockerGameEvent
            {
                Origin = ParseVictimClient(scriptEvent, data),
                IsStore = false,
                WeaponName = weaponName
            },
            _ => throw new ArgumentException($"Unknown locker action: {action}")
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

    // ZE;{revived};revive;{reviver guid;cnum;team;name}   — co-op revive
    // ZE;{revived};revive;self                             — self-revive
    //   T5: solo Quick Revive auto
    //   T6: solo QR auto, Who's Who
    //   T7: solo QR auto, Self Revive gobblegum
    private static GameEventV2 ParseZeRevive(GameScriptEvent scriptEvent, string[] data)
    {
        var revived = ParseVictimClient(scriptEvent, data);

        if (data.Length > 5 && string.Equals(data[5], "self", StringComparison.Ordinal))
        {
            return new PlayerRevivedGameEvent
            {
                Origin = revived,
                Target = revived,
                IsSelfRevive = true
            };
        }

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

    // ZE;{player};gum;{action};{bgbName};[cost] — T7 only.
    //   activate: player consumed an "activated" limit_type gum (Perkaholic etc.)
    //   take:     player grabbed a gum from a BGB machine
    //   leave:    player paid but didn't grab — cost forfeited (ghost-ball excluded)
    // Auto-trigger gum types (time/rounds/event-limited) don't fire bgb_activation
    // and aren't surfaced.
    private static GameEventV2 ParseZeGobbleGum(GameScriptEvent scriptEvent, string[] data)
    {
        var action = data.Length > 5 ? data[5] : string.Empty;
        var gumName = data.Length > 6 ? data[6] : string.Empty;

        return action switch
        {
            "activate" => new GobbleGumActivatedGameEvent
            {
                Origin = ParseVictimClient(scriptEvent, data),
                GumName = gumName
            },
            "take" => new GobbleGumTakenGameEvent
            {
                Origin = ParseVictimClient(scriptEvent, data),
                GumName = gumName,
                Cost = data.Length > 7 ? Convert.ToInt32(data[7]) : 0
            },
            "leave" => new GobbleGumAbandonedGameEvent
            {
                Origin = ParseVictimClient(scriptEvent, data),
                GumName = gumName,
                Cost = data.Length > 7 ? Convert.ToInt32(data[7]) : 0
            },
            _ => throw new ArgumentException($"Unknown ZE gum action: {action}")
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
