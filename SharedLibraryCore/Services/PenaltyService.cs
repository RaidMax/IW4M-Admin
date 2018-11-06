using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Objects;
using static SharedLibraryCore.Database.Models.EFClient;

namespace SharedLibraryCore.Services
{
    public class PenaltyService : Interfaces.IEntityService<EFPenalty>
    {
        public async Task<EFPenalty> Create(EFPenalty newEntity)
        {
            using (var context = new DatabaseContext())
            {
                // create the actual EFPenalty
                EFPenalty addedEntity = new EFPenalty()
                {
                    OffenderId = newEntity.Offender.ClientId,
                    PunisherId = newEntity.Punisher.ClientId,
                    LinkId = newEntity.Link.AliasLinkId,
                    Type = newEntity.Type,
                    Expires = newEntity.Expires,
                    Offense = newEntity.Offense,
                    When = newEntity.When,
                    AutomatedOffense = newEntity.AutomatedOffense
                };

                // make bans propogate to all aliases
                if (addedEntity.Type == Objects.Penalty.PenaltyType.Ban)
                {
                    await context.Clients
                        .Where(c => c.AliasLinkId == addedEntity.LinkId)
                        .ForEachAsync(c => c.Level = Permission.Banned);
                }

                // make flags propogate to all aliases
                else if (addedEntity.Type == Objects.Penalty.PenaltyType.Flag)
                {
                    await context.Clients
                      .Where(c => c.AliasLinkId == addedEntity.LinkId)
                      .ForEachAsync(c => c.Level = Permission.Flagged);
                }

                context.Penalties.Add(addedEntity);
                await context.SaveChangesAsync();
                return addedEntity;
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

        public async Task<IList<EFPenalty>> GetClientPenaltiesAsync(int clientId)
        {
            using (var context = new DatabaseContext(true))
                return await context.Penalties
                    .Where(p => p.OffenderId == clientId)
                    .Where(p => p.Active)
                    .Include(p => p.Offender.CurrentAlias)
                    .Include(p => p.Punisher.CurrentAlias)
                    .ToListAsync();
        }

        /// <summary>
        /// Get a read-only copy of client penalties
        /// </summary>
        /// <param name="clientI"></param>
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
                                              Type = penalty.Type.ToString(),
                                              TimeRemaining = penalty.Expires.HasValue ?  (now > penalty.Expires ? "" : penalty.Expires.ToString()) : DateTime.MaxValue.ToString(),
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
                        if (((PenaltyInfo)p.Value).Type.Length < 2)
                            ((PenaltyInfo)p.Value).Type = ((Penalty.PenaltyType)Convert.ToInt32(((PenaltyInfo)p.Value).Type)).ToString();

                        var pi = ((PenaltyInfo)p.Value);
                        if (pi.TimeRemaining?.Length > 0)
                            pi.TimeRemaining = (DateTime.Parse(((PenaltyInfo)p.Value).TimeRemaining) - now).TimeSpanText();

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
                                              Type = penalty.Type.ToString(),
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
                        if (((PenaltyInfo)p.Value).Type.Length < 2)
                            ((PenaltyInfo)p.Value).Type = ((Penalty.PenaltyType)Convert.ToInt32(((PenaltyInfo)p.Value).Type)).ToString();
                    });

                    return list;
                }
            }
        }

        public async Task<List<EFPenalty>> GetActivePenaltiesAsync(int linkId, int ip = 0)
        {
            var now = DateTime.UtcNow;

            using (var context = new DatabaseContext(true))
            {
                var iqPenalties = context.Penalties
                    .Where(p => p.LinkId == linkId ||
                         p.Link.Children.Any(a => a.IPAddress == ip))
                    .Where(p => p.Type == Penalty.PenaltyType.TempBan ||
                         p.Type == Penalty.PenaltyType.Ban ||
                         p.Type == Penalty.PenaltyType.Flag)
                    .Where(p => p.Active)
                    .Where(p => p.Expires == null || p.Expires > now);
       
#if DEBUG == true
                var penaltiesSql = iqPenalties.ToSql();
#endif

                var activePenalties = await iqPenalties.ToListAsync();
                // this is a bit more performant in memory (ordering)
                return activePenalties.OrderByDescending(p =>p.When).ToList();
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
