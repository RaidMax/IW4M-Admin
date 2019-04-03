using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using static SharedLibraryCore.Database.Models.EFClient;

namespace SharedLibraryCore.Services
{
    public class PenaltyService : Interfaces.IEntityService<EFPenalty>
    {
        public async Task<EFPenalty> Create(EFPenalty newEntity)
        {
            using (var context = new DatabaseContext())
            {
                var penalty = new EFPenalty()
                {
                    Active = true,
                    OffenderId = newEntity.Offender.ClientId,
                    PunisherId = newEntity.Punisher.ClientId,
                    LinkId = newEntity.Link.AliasLinkId,
                    Type = newEntity.Type,
                    Expires = newEntity.Expires,
                    Offense = newEntity.Offense,
                    When = DateTime.UtcNow,
                    AutomatedOffense = newEntity.AutomatedOffense,
                    IsEvadedOffense = newEntity.IsEvadedOffense
                };

                newEntity.Offender.ReceivedPenalties?.Add(penalty);
                context.Penalties.Add(penalty);
                await context.SaveChangesAsync();
            }

            return newEntity;
        }

        public Task<EFPenalty> CreateProxy()
        {
            throw new NotImplementedException();
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

        public Task<EFPenalty> GetUnique(long entityProperty)
        {
            throw new NotImplementedException();
        }

        public Task<EFPenalty> Update(EFPenalty entity)
        {
            throw new NotImplementedException();
        }

        public async Task<IList<EFPenalty>> GetClientPenaltiesAsync(int clientId)
        {
            using (var context = new DatabaseContext(true))
            {
                return await context.Penalties
                    .Where(p => p.OffenderId == clientId)
                    .Where(p => p.Active)
                    .Include(p => p.Offender.CurrentAlias)
                    .Include(p => p.Punisher.CurrentAlias)
                    .ToListAsync();
            }
        }

        public async Task<IList<PenaltyInfo>> GetRecentPenalties(int count, int offset, Penalty.PenaltyType showOnly = Penalty.PenaltyType.Any)
        {
            using (var context = new DatabaseContext(true))
            {
                var iqPenalties = context.Penalties
                    .Where(p => showOnly == Penalty.PenaltyType.Any ? p.Type != Penalty.PenaltyType.Any : p.Type == showOnly)
                    .Where(p => p.Active)
                    .OrderByDescending(p => p.When)
                    .Skip(offset)
                    .Take(count)
                     .Select(_penalty => new PenaltyInfo()
                     {
                         Id = _penalty.PenaltyId,
                         Offense = _penalty.Offense,
                         AutomatedOffense = _penalty.AutomatedOffense,
                         OffenderId = _penalty.OffenderId,
                         OffenderName = _penalty.Offender.CurrentAlias.Name,
                         PunisherId = _penalty.PunisherId,
                         PunisherName = _penalty.Punisher.CurrentAlias.Name,
                         PunisherLevel = _penalty.Punisher.Level,
                         PenaltyType = _penalty.Type,
                         Expires = _penalty.Expires,
                         TimePunished = _penalty.When,
                         IsEvade = _penalty.IsEvadedOffense
                     });

#if DEBUG == true
                var querySql = iqPenalties.ToSql();
#endif
                return await iqPenalties.ToListAsync();
            }
        }

        /// <summary>
        /// retrieves penalty information for meta service
        /// </summary>
        /// <param name="clientId">database id of the client</param>
        /// <param name="count">how many items to retrieve</param>
        /// <param name="offset">not used</param>
        /// <param name="startAt">retreive penalties older than this</param>
        /// <returns></returns>
        public async Task<IList<PenaltyInfo>> GetClientPenaltyForMetaAsync(int clientId, int count, int offset, DateTime? startAt)
        {
            using (var ctx = new DatabaseContext(true))
            {
                var iqPenalties = ctx.Penalties.AsNoTracking()
                    .Where(_penalty => _penalty.Active)
                    .Where(_penalty => _penalty.OffenderId == clientId || _penalty.PunisherId == clientId)
                    .Where(_penalty => _penalty.When < startAt)
                    .OrderByDescending(_penalty => _penalty.When)
                    .Skip(offset)
                    .Take(count)
                    .Select(_penalty => new PenaltyInfo()
                    {
                        Id = _penalty.PenaltyId,
                        Offense = _penalty.Offense,
                        AutomatedOffense = _penalty.AutomatedOffense,
                        OffenderId = _penalty.OffenderId,
                        OffenderName = _penalty.Offender.CurrentAlias.Name,
                        PunisherId = _penalty.PunisherId,
                        PunisherName = _penalty.Punisher.CurrentAlias.Name,
                        PunisherLevel = _penalty.Punisher.Level,
                        PenaltyType = _penalty.Type,
                        Expires = _penalty.Expires,
                        TimePunished = _penalty.When,
                        IsEvade = _penalty.IsEvadedOffense
                    });

#if DEBUG == true
                var querySql = iqPenalties.ToSql();
#endif

                return await iqPenalties.ToListAsync();
            }
        }

        public async Task<List<EFPenalty>> GetActivePenaltiesAsync(int linkId, int? ip = null)
        {
            var now = DateTime.UtcNow;

            Expression<Func<EFPenalty, bool>> filter = (p) => new Penalty.PenaltyType[]
                         {
                            Penalty.PenaltyType.TempBan,
                            Penalty.PenaltyType.Ban,
                            Penalty.PenaltyType.Flag
                         }.Contains(p.Type) &&
                         p.Active &&
                         (p.Expires == null || p.Expires > now);

            using (var context = new DatabaseContext(true))
            {
                var iqLinkPenalties = context.Penalties
                    .Where(p => p.LinkId == linkId)
                    .Where(filter);

                var iqIPPenalties = context.Aliases
                    .Where(a => a.IPAddress != null & a.IPAddress == ip)
                    .SelectMany(a => a.Link.ReceivedPenalties)
                    .Where(filter);

#if DEBUG == true
                var penaltiesSql = iqLinkPenalties.ToSql();
                var ipPenaltiesSql = iqIPPenalties.ToSql();
#endif

                var activePenalties = (await iqLinkPenalties.ToListAsync())
                    .Union(await iqIPPenalties.ToListAsync())
                    .Distinct();

                // this is a bit more performant in memory (ordering)
                return activePenalties.OrderByDescending(p => p.When).ToList();
            }
        }

        public async Task RemoveActivePenalties(int aliasLinkId, EFClient origin)
        {
            using (var context = new DatabaseContext())
            {
                var now = DateTime.UtcNow;
                var penalties = context.Penalties
                    .Include(p => p.Link.Children)
                    .Where(p => p.LinkId == aliasLinkId)
                    .Where(p => p.Expires > now || p.Expires == null);

                await penalties.ForEachAsync(p =>
                {
                    p.Active = false;
                    
                });

                await context.SaveChangesAsync();
            }
        }
    }
}
