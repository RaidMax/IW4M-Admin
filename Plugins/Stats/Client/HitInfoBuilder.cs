using System;
using System.Linq;
using Data.Models;
using IW4MAdmin.Plugins.Stats.Client.Game;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Interfaces;
using Stats.Client.Abstractions;
using Stats.Client.Game;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Stats.Client;

public class HitInfoBuilder : IHitInfoBuilder
{
    private readonly IWeaponNameParser _weaponNameParser;
    private readonly ILogger _logger;
    private const int MaximumDamage = 1000;

    public HitInfoBuilder(ILogger<HitInfoBuilder> logger, IWeaponNameParser weaponNameParser)
    {
        _weaponNameParser = weaponNameParser;
        _logger = logger;
    }

    public HitInfo Build(string[] log, ParserRegex parserRegex, int entityId, bool isSelf, bool isVictim,
        Reference.Game gameName)
    {
        var eventType = log[(uint)ParserRegex.GroupType.EventType].First();
        HitType hitType;

        if (isVictim)
        {
            if (isSelf)
            {
                hitType = HitType.Suicide;
            }

            else
            {
                hitType = eventType == 'D' ? HitType.WasDamaged : HitType.WasKilled;
            }
        }

        else
        {
            hitType = eventType == 'D' ? HitType.Damage : HitType.Kill;
        }

        var damage = 0;
        try
        {
            damage = Math.Min(MaximumDamage,
                log.Length > parserRegex.GroupMapping[ParserRegex.GroupType.Damage]
                    ? int.Parse(log[parserRegex.GroupMapping[ParserRegex.GroupType.Damage]])
                    : 0);
        }
        catch
        {
            // ignored
        }

        var hitInfo = new HitInfo()
        {
            EntityId = entityId,
            IsVictim = isVictim,
            HitType = hitType,
            Damage = damage,
            Location = log.Length > parserRegex.GroupMapping[ParserRegex.GroupType.HitLocation]
                ? log[parserRegex.GroupMapping[ParserRegex.GroupType.HitLocation]]
                : "Unknown",
            Weapon = log.Length > parserRegex.GroupMapping[ParserRegex.GroupType.Weapon]
                ? _weaponNameParser.Parse(log[parserRegex.GroupMapping[ParserRegex.GroupType.Weapon]], gameName)
                : new WeaponInfo { Name = "Unknown" },
            MeansOfDeath = log.Length > parserRegex.GroupMapping[ParserRegex.GroupType.MeansOfDeath]
                ? log[parserRegex.GroupMapping[ParserRegex.GroupType.MeansOfDeath]]
                : "Unknown",
            Game = gameName
        };

        return hitInfo;
    }
}
