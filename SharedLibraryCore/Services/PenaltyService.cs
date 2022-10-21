using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Services
{
    public class PenaltyService : IEntityService<EFPenalty>
    {
        private readonly ApplicationConfiguration _appConfig;
        private readonly IDatabaseContextFactory _contextFactory;

        public PenaltyService(IDatabaseContextFactory contextFactory, ApplicationConfiguration appConfig)
        {
            _contextFactory = contextFactory;
            _appConfig = appConfig;
        }

        public virtual async Task<EFPenalty> Create(EFPenalty newEntity)
        {
            await using var context = _contextFactory.CreateContext();
            var penalty = new EFPenalty
            {
                Active = true,
                OffenderId = newEntity.Offender.ClientId,
                PunisherId = newEntity.Punisher.ClientId,
                LinkId = newEntity.Link?.AliasLinkId,
                Type = newEntity.Type,
                Expires = newEntity.Expires,
                Offense = newEntity.Offense,
                When = DateTime.UtcNow,
                AutomatedOffense = newEntity.AutomatedOffense ??
                                   newEntity.Punisher.AdministeredPenalties?.FirstOrDefault()?.AutomatedOffense,
                IsEvadedOffense = newEntity.IsEvadedOffense
            };
            
            if (LinkedPenalties.Contains(newEntity.Type))
            {
                var penaltyIdentifiers = new EFPenaltyIdentifier
                {
                    Penalty = penalty,
                    NetworkId = newEntity.Offender.NetworkId,
                    IPv4Address = newEntity.Offender.CurrentAlias.IPAddress
                };

                context.PenaltyIdentifiers.Add(penaltyIdentifiers);
            }

            context.Penalties.Add(penalty);
            await context.SaveChangesAsync();

            return newEntity;
        }

        public async Task CreatePenaltyIdentifier(int penaltyId, long networkId, int ipv4Address)
        {
            await using var context = _contextFactory.CreateContext();
            var penaltyIdentifiers = new EFPenaltyIdentifier
            {
                PenaltyId = penaltyId,
                NetworkId = networkId,
                IPv4Address = ipv4Address
            };

            context.PenaltyIdentifiers.Add(penaltyIdentifiers);
            await context.SaveChangesAsync();
        }

        public Task<EFPenalty> Delete(EFPenalty entity)
        {
            throw new NotImplementedException();
        }

        public async Task<IList<EFPenalty>> Find(Func<EFPenalty, bool> expression)
        {
            throw await Task.FromResult(new Exception());
        }

        public Task<EFPenalty> Get(int entityID)
        {
            throw new NotImplementedException();
        }

        public Task<EFPenalty> GetUnique(long entityProperty, object altKey)
        {
            throw new NotImplementedException();
        }

        public Task<EFPenalty> Update(EFPenalty entity)
        {
            throw new NotImplementedException();
        }

        public async Task<IList<PenaltyInfo>> GetRecentPenalties(int count, int offset,
            EFPenalty.PenaltyType showOnly = EFPenalty.PenaltyType.Any, bool ignoreAutomated = true)
        {
            await using var context = _contextFactory.CreateContext(false);
            var iqPenalties = context.Penalties
                .Where(p => showOnly == EFPenalty.PenaltyType.Any
                    ? p.Type != EFPenalty.PenaltyType.Any
                    : p.Type == showOnly)
                .Where(_penalty => !ignoreAutomated || _penalty.PunisherId != 1)
                .OrderByDescending(p => p.When)
                .Skip(offset)
                .Take(count)
                .Select(_penalty => new PenaltyInfo
                {
                    Id = _penalty.PenaltyId,
                    Offense = _penalty.Offense,
                    AutomatedOffense = _penalty.AutomatedOffense,
                    OffenderId = _penalty.OffenderId,
                    OffenderName = _penalty.Offender.CurrentAlias.Name,
                    OffenderLevel = _penalty.Offender.Level,
                    PunisherId = _penalty.PunisherId,
                    PunisherName = _penalty.Punisher.CurrentAlias.Name,
                    PunisherLevel = _penalty.Punisher.Level,
                    PenaltyType = _penalty.Type,
                    Expires = _penalty.Expires,
                    TimePunished = _penalty.When,
                    IsEvade = _penalty.IsEvadedOffense
                });

            return await iqPenalties.ToListAsync();
        }

        private static readonly EFPenalty.PenaltyType[] LinkedPenalties =
            { EFPenalty.PenaltyType.Ban, EFPenalty.PenaltyType.Flag, EFPenalty.PenaltyType.TempBan, EFPenalty.PenaltyType.TempMute, EFPenalty.PenaltyType.Mute };

        private static readonly Expression<Func<EFPenalty, bool>> Filter = p =>
            LinkedPenalties.Contains(p.Type) && p.Active && (p.Expires == null || p.Expires > DateTime.UtcNow);

        private static readonly Expression<Func<EFPenaltyIdentifier, bool>> FilterById = pi =>
            LinkedPenalties.Contains(pi.Penalty.Type) && pi.Penalty.Active &&
            (pi.Penalty.Expires == null || pi.Penalty.Expires > DateTime.UtcNow);

        public async Task<List<EFPenalty>> GetActivePenaltiesAsync(int linkId, int currentAliasId, long networkId, Reference.Game game,
            int? ip = null)
        {
            var penaltiesByIdentifier = await GetActivePenaltiesByIdentifier(ip, networkId, game);

            if (penaltiesByIdentifier.Any())
            {
                return penaltiesByIdentifier;
            }

            await using var context = _contextFactory.CreateContext(false);

            IQueryable<EFPenalty> iqIpPenalties;

            if (_appConfig.EnableImplicitAccountLinking)
            {
                iqIpPenalties = context.Aliases
                    .Where(a => a.IPAddress != null && a.IPAddress == ip)
                    .SelectMany(a => a.Link.ReceivedPenalties)
                    .Where(Filter);
            }
            else
            {
                var usedIps = await context.Aliases.AsNoTracking()
                    .Where(alias =>
                        (alias.LinkId == linkId || alias.AliasId == currentAliasId) && alias.IPAddress != null)
                    .Select(alias => alias.IPAddress).ToListAsync();

                var aliasedIds = await context.Aliases.AsNoTracking().Where(alias => usedIps.Contains(alias.IPAddress))
                    .Select(alias => alias.LinkId)
                    .ToListAsync();

                iqIpPenalties = context.Penalties.AsNoTracking()
                    .Where(penalty => aliasedIds.Contains(penalty.LinkId ?? -1) || penalty.LinkId == linkId)
                    .Where(Filter);
            }

            var activeIpPenalties = await iqIpPenalties.ToListAsync();
            var activePenalties = activeIpPenalties.Distinct();

            // this is a bit more performant in memory (ordering)
            return activePenalties.OrderByDescending(p => p.When).ToList();
        }

        public async Task<List<EFPenalty>> GetActivePenaltiesByIdentifier(int? ip, long networkId, Reference.Game game)
        {
            await using var context = _contextFactory.CreateContext(false);

            var activePenaltiesIds = context.PenaltyIdentifiers.Where(identifier =>
                    identifier.IPv4Address != null && identifier.IPv4Address == ip || identifier.NetworkId == networkId && identifier.Penalty.Offender.GameName == game)
                .Where(FilterById);
            return await activePenaltiesIds.Select(ids => ids.Penalty).ToListAsync();
        }
        
        public async Task<List<EFPenalty>> ActivePenaltiesByRecentIdentifiers(int linkId)
        {
            await using var context = _contextFactory.CreateContext(false);

            var recentlyUsedIps = await context.Aliases.Where(alias => alias.LinkId == linkId)
                .Where(alias => alias.IPAddress != null)
                .Where(alias => alias.DateAdded >= DateTime.UtcNow - _appConfig.RecentAliasIpLinkTimeLimit)
                .Select(alias => alias.IPAddress).ToListAsync();

            if (!recentlyUsedIps.Any())
            {
                return new List<EFPenalty>();
            }

            var activePenaltiesIds = context.PenaltyIdentifiers
                .Where(identifier => recentlyUsedIps.Contains(identifier.IPv4Address))
                .Where(FilterById);

            return await activePenaltiesIds.Select(ids => ids.Penalty).ToListAsync();
        }

        public virtual async Task RemoveActivePenalties(int aliasLinkId, long networkId, Reference.Game game, int? ipAddress = null)
        {
            await using var context = _contextFactory.CreateContext();
            var now = DateTime.UtcNow;

            var activePenalties = await GetActivePenaltiesByIdentifier(ipAddress, networkId, game);

            if (activePenalties.Any())
            {
                var ids = activePenalties.Select(penalty => penalty.PenaltyId);
                await context.Penalties.Where(penalty => ids.Contains(penalty.PenaltyId))
                    .ForEachAsync(penalty =>
                    {
                        penalty.Active = false;
                        penalty.Expires = now;
                    });
                await context.SaveChangesAsync();
                return;
            }

            var penaltiesByLink = context.Penalties
                .Where(p => p.LinkId == aliasLinkId)
                .Where(p => p.Expires > now || p.Expires == null);

            var penaltiesByIp = context.Penalties
                .Where(p => p.Offender.CurrentAlias.IPAddress != null && p.Offender.CurrentAlias.IPAddress == null)
                .Where(p => p.Expires > now || p.Expires == null);

            await penaltiesByLink.Union(penaltiesByIp).Distinct().ForEachAsync(p =>
            {
                p.Active = false;
                p.Expires = now;
            });

            await context.SaveChangesAsync();
        }
    }
}
