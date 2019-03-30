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
                // make bans propogate to all aliases
                if (newEntity.Type == Penalty.PenaltyType.Ban)
                {
                    await context.Clients
                        .Include(c => c.ReceivedPenalties)
                        .Where(c => c.AliasLinkId == newEntity.Link.AliasLinkId)
                        .ForEachAsync(c =>
                        {
                            if (c.Level != Permission.Banned)
                            {
                                c.Level = Permission.Banned;
                                c.ReceivedPenalties.Add(new EFPenalty()
                                {
                                    Active = true,
                                    OffenderId = c.ClientId,
                                    PunisherId = newEntity.Punisher.ClientId,
                                    LinkId = c.AliasLinkId,
                                    Type = newEntity.Type,
                                    Expires = newEntity.Expires,
                                    Offense = newEntity.Offense,
                                    When = DateTime.UtcNow,
                                    AutomatedOffense = newEntity.AutomatedOffense,
                                    IsEvadedOffense = newEntity.IsEvadedOffense
                                });
                            }
                        });
                }

                // make flags propogate to all aliases
                else if (newEntity.Type == Penalty.PenaltyType.Flag)
                {
                    await context.Clients
                      .Include(c => c.ReceivedPenalties)
                      .Where(c => c.AliasLinkId == newEntity.Link.AliasLinkId)
                      .ForEachAsync(c =>
                      {
                          if (c.Level != Permission.Flagged)
                          {
                              c.Level = Permission.Flagged;
                              c.ReceivedPenalties.Add(new EFPenalty()
                              {
                                  Active = true,
                                  OffenderId = c.ClientId,
                                  PunisherId = newEntity.Punisher.ClientId,
                                  LinkId = c.AliasLinkId,
                                  Type = newEntity.Type,
                                  Expires = newEntity.Expires,
                                  Offense = newEntity.Offense,
                                  When = DateTime.UtcNow,
                                  AutomatedOffense = newEntity.AutomatedOffense,
                                  IsEvadedOffense = newEntity.IsEvadedOffense
                              });
                          }
                      });
                }

                // we just want to add it to the database
                else
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
                }

                await context.SaveChangesAsync();
                return newEntity;
            }
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

        public async Task<IList<EFPenalty>> GetRecentPenalties(int count, int offset, Penalty.PenaltyType showOnly = Penalty.PenaltyType.Any)
        {
            using (var context = new DatabaseContext(true))
            {
                return await context.Penalties
                   .Include(p => p.Offender.CurrentAlias)
                   .Include(p => p.Punisher.CurrentAlias)
                   .Where(p => showOnly == Penalty.PenaltyType.Any ? p.Type != Penalty.PenaltyType.Any : p.Type == showOnly)
                   .Where(p => p.Active)
                   .OrderByDescending(p => p.When)
                   .Skip(offset)
                   .Take(count)
                   .ToListAsync();
            }
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

        public async Task<IList<EFPenalty>> GetAllClientPenaltiesAsync(int clientId, int count, int offset, DateTime? startAt)
        {
            using (var ctx = new DatabaseContext(true))
            {
                var iqPenalties = ctx.Penalties.AsNoTracking()
                    .Include(_penalty => _penalty.Offender.CurrentAlias)
                    .Include(_penalty => _penalty.Punisher.CurrentAlias)
                    .Where(_penalty => _penalty.Active)
                    .Where(_penalty => _penalty.OffenderId == clientId || _penalty.PunisherId == clientId)
                    .Where(_penalty => _penalty.When < startAt)
                    .OrderByDescending(_penalty => _penalty.When)
                    .Skip(offset)
                    .Take(count);

                return await iqPenalties.ToListAsync();
            }
        }

        /// <summary>
        /// Get a read-only copy of client penalties
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="victim">Retreive penalties for clients receiving penalties, other wise given</param>
        /// <returns></returns>
        public async Task<List<ProfileMeta>> ReadGetClientPenaltiesAsync(int clientId, bool victim = true)
        {
            using (var context = new DatabaseContext(true))
            {
                // todo: clean this up
                if (victim)
                {
                    var now = DateTime.UtcNow;
                    var iqPenalties = from penalty in context.Penalties.AsNoTracking()
                                      where penalty.OffenderId == clientId
                                      join victimClient in context.Clients.AsNoTracking()
                                      on penalty.OffenderId equals victimClient.ClientId
                                      join victimAlias in context.Aliases.AsNoTracking()
                                      on victimClient.CurrentAliasId equals victimAlias.AliasId
                                      join punisherClient in context.Clients.AsNoTracking()
                                      on penalty.PunisherId equals punisherClient.ClientId
                                      join punisherAlias in context.Aliases.AsNoTracking()
                                      on punisherClient.CurrentAliasId equals punisherAlias.AliasId
                                      //orderby penalty.When descending
                                      select new ProfileMeta()
                                      {
                                          Key = "Event.Penalty",
                                          Value = new PenaltyInfo
                                          {
                                              Id = penalty.PenaltyId,
                                              OffenderName = victimAlias.Name,
                                              OffenderId = victimClient.ClientId,
                                              PunisherName = punisherAlias.Name,
                                              PunisherId = penalty.PunisherId,
                                              Offense = penalty.Offense,
                                              PenaltyType = penalty.Type.ToString(),
                                              TimeRemaining = penalty.Expires.HasValue ? (now > penalty.Expires ? "" : penalty.Expires.ToString()) : DateTime.MaxValue.ToString(),
                                              AutomatedOffense = penalty.AutomatedOffense,
                                              Expired = penalty.Expires.HasValue && penalty.Expires <= DateTime.UtcNow
                                          },
                                          When = penalty.When,
                                          Sensitive = penalty.Type == Penalty.PenaltyType.Flag
                                      };
                    // fixme: is this good and fast?
                    var list = await iqPenalties.ToListAsync();
                    list.ForEach(p =>
                    {
                        // todo: why does this have to be done?
                        if (((PenaltyInfo)p.Value).PenaltyType.Length < 2)
                        {
                            ((PenaltyInfo)p.Value).PenaltyType = ((Penalty.PenaltyType)Convert.ToInt32(((PenaltyInfo)p.Value).PenaltyType)).ToString();
                        }

                        var pi = ((PenaltyInfo)p.Value);
                        if (pi.TimeRemaining?.Length > 0)
                        {
                            pi.TimeRemaining = (DateTime.Parse(((PenaltyInfo)p.Value).TimeRemaining) - now).TimeSpanText();

                            if (!pi.Expired)
                            {
                                pi.TimeRemaining = $"{pi.TimeRemaining} {Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PENALTY_TEMPLATE_REMAINING"]}";
                            }
                        }
                    });
                    return list;
                }

                else
                {
                    var iqPenalties = from penalty in context.Penalties.AsNoTracking()
                                      where penalty.PunisherId == clientId
                                      join victimClient in context.Clients.AsNoTracking()
                                      on penalty.OffenderId equals victimClient.ClientId
                                      join victimAlias in context.Aliases
                                      on victimClient.CurrentAliasId equals victimAlias.AliasId
                                      join punisherClient in context.Clients
                                      on penalty.PunisherId equals punisherClient.ClientId
                                      join punisherAlias in context.Aliases
                                      on punisherClient.CurrentAliasId equals punisherAlias.AliasId
                                      //orderby penalty.When descending
                                      select new ProfileMeta()
                                      {
                                          Key = "Event.Penalty",
                                          Value = new PenaltyInfo
                                          {
                                              Id = penalty.PenaltyId,
                                              OffenderName = victimAlias.Name,
                                              OffenderId = victimClient.ClientId,
                                              PunisherName = punisherAlias.Name,
                                              PunisherId = penalty.PunisherId,
                                              Offense = penalty.Offense,
                                              PenaltyType = penalty.Type.ToString(),
                                              AutomatedOffense = penalty.AutomatedOffense
                                          },
                                          When = penalty.When,
                                          Sensitive = penalty.Type == Penalty.PenaltyType.Flag
                                      };
                    // fixme: is this good and fast?
                    var list = await iqPenalties.ToListAsync();

                    list.ForEach(p =>
                    {
                        // todo: why does this have to be done?
                        if (((PenaltyInfo)p.Value).PenaltyType.Length < 2)
                        {
                            ((PenaltyInfo)p.Value).PenaltyType = ((Penalty.PenaltyType)Convert.ToInt32(((PenaltyInfo)p.Value).PenaltyType)).ToString();
                        }
                    });

                    return list;
                }
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

        public async Task RemoveActivePenalties(int aliasLinkId)
        {
            using (var context = new DatabaseContext())
            {
                var now = DateTime.UtcNow;
                var penalties = await context.Penalties
                    .Include(p => p.Link.Children)
                    .Where(p => p.LinkId == aliasLinkId)
                    .Where(p => p.Expires > now || p.Expires == null)
                    .ToListAsync();

                penalties.ForEach(async p =>
                {
                    p.Active = false;
                    // reset the player levels
                    if (p.Type == Penalty.PenaltyType.Ban)
                    {
                        using (var internalContext = new DatabaseContext())
                        {
                            await internalContext.Clients
                                .Where(c => c.AliasLinkId == p.LinkId)
                                .ForEachAsync(c => c.Level = EFClient.Permission.User);
                            await internalContext.SaveChangesAsync();
                        }
                    }
                });

                await context.SaveChangesAsync();
            }
        }
    }
}
