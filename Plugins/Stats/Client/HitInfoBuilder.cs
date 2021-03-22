using System;
using System.Linq;
using Data.Models;
using IW4MAdmin.Plugins.Stats.Client.Game;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using Stats.Client.Abstractions;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Stats.Client
{
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
        
        public HitInfo Build(string[] log, int entityId, bool isSelf, bool isVictim, Server.Game gameName)
        {
            var eventType = log[(uint) ParserRegex.GroupType.EventType].First();
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

            var hitInfo = new HitInfo()
            {
                EntityId = entityId,
                IsVictim = isVictim,
                HitType = hitType,
                Damage = Math.Min(MaximumDamage, int.Parse(log[(uint) ParserRegex.GroupType.Damage])),
                Location = log[(uint) ParserRegex.GroupType.HitLocation],
                Weapon = _weaponNameParser.Parse(log[(uint) ParserRegex.GroupType.Weapon], gameName),
                MeansOfDeath = log[(uint)ParserRegex.GroupType.MeansOfDeath],
                Game = (Reference.Game)gameName
            };

            //_logger.LogDebug("Generated new hitInfo {@hitInfo}", hitInfo);
            return hitInfo;
        }
    }
}