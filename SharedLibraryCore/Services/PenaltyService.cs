using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using System.Linq.Expressions;
using SharedLibraryCore.Dtos;
using Microsoft.EntityFrameworkCore;

namespace SharedLibraryCore.Services
{
    public class PenaltyService : Interfaces.IEntityService<EFPenalty>
    {
        public async Task<EFPenalty> Create(EFPenalty entity)
        {
            using (var context = new DatabaseContext())
            {
                entity.Offender = context.Clients.Single(e => e.ClientId == entity.Offender.ClientId);
                entity.Punisher = context.Clients.Single(e => e.ClientId == entity.Punisher.ClientId);
                entity.Link = context.AliasLinks.Single(l => l.AliasLinkId == entity.Link.AliasLinkId);

                if (entity.Expires == DateTime.MaxValue)
                    entity.Expires = DateTime.Parse(System.Data.SqlTypes.SqlDateTime.MaxValue.ToString());

                // make bans propogate to all aliases
                if (entity.Type == Objects.Penalty.PenaltyType.Ban)
                {
                    await context.Clients
                        .Where(c => c.AliasLinkId == entity.Link.AliasLinkId)
                        .ForEachAsync(c => c.Level = Objects.Player.Permission.Banned);
                }

                // make flags propogate to all aliases
                else if (entity.Type == Objects.Penalty.PenaltyType.Flag)
                {
                    await context.Clients
                      .Where(c => c.AliasLinkId == entity.Link.AliasLinkId)
                      .ForEachAsync(c => c.Level = Objects.Player.Permission.Flagged);
                }

                context.Penalties.Add(entity);
                await context.SaveChangesAsync();
                return entity;
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
            /*
            return await Task.FromResult(new List<EFPenalty>());
            // fixme: this is so slow!
            return await Task.Run(() =>
            {
                using (var context = new DatabaseContext())
                    return context.Penalties
                    .Include(p => p.Offender)
                    .Include(p => p.Punisher)
                    .Where(expression)
                    .Where(p => p.Active)
                    .ToList();
            });*/
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

        public async Task<IList<EFPenalty>> GetRecentPenalties(int count, int offset)
        {
            using (var context = new DatabaseContext())
                return await context.Penalties
                    .AsNoTracking()
                   .Include(p => p.Offender.CurrentAlias)
                   .Include(p => p.Punisher.CurrentAlias)
                   .Where(p => p.Active)
                   .OrderByDescending(p => p.When)
                   .Skip(offset)
                   .Take(count)
                  .ToListAsync();
        }

        public async Task<IList<EFPenalty>> GetClientPenaltiesAsync(int clientId)
        {
            using (var context = new DatabaseContext())
                return await context.Penalties
                    .AsNoTracking()
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
            using (var context = new DatabaseContext())
            {
                /*context.Configuration.LazyLoadingEnabled = false;
                context.Configuration.ProxyCreationEnabled = false;
                context.Configuration.AutoDetectChangesEnabled = false;*/

                if (victim)
                {
                    var iqPenalties = from penalty in context.Penalties.AsNoTracking()
                                      where penalty.OffenderId == clientId
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
                                              OffenderName = victimAlias.Name,
                                              OffenderId = victimClient.ClientId,
                                              PunisherName = punisherAlias.Name,
                                              PunisherId = penalty.PunisherId,
                                              Offense = penalty.Offense,
                                              Type = penalty.Type.ToString()
                                          },
                                          When = penalty.When,
                                          Sensitive = penalty.Type == Objects.Penalty.PenaltyType.Flag
                                      };
                    // fixme: is this good and fast?
                    return await iqPenalties.ToListAsync();
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
                                              OffenderName = victimAlias.Name,
                                              OffenderId = victimClient.ClientId,
                                              PunisherName = punisherAlias.Name,
                                              PunisherId = penalty.PunisherId,
                                              Offense = penalty.Offense,
                                              Type = penalty.Type.ToString()
                                          },
                                          When = penalty.When
                                      };
                    // fixme: is this good and fast?
                    return await iqPenalties.ToListAsync();
                }


            }
        }

        public async Task<List<EFPenalty>> GetActivePenaltiesAsync(int aliasId)
        {
            using (var context = new DatabaseContext())
            {
                var iqPenalties = from link in context.AliasLinks
                                  where link.AliasLinkId == aliasId
                                  join penalty in context.Penalties
                                  on link.AliasLinkId equals penalty.LinkId
                                  where penalty.Active
                                  select penalty;
                return await iqPenalties.ToListAsync();
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
                    .Where(p => p.Expires > now)
                    .ToListAsync();

                penalties.ForEach(async p =>
                {
                    p.Active = false;
                    // reset the player levels
                    if (p.Type == Objects.Penalty.PenaltyType.Ban)
                        await context.Clients
                            .Where(c => c.AliasLinkId == p.LinkId)
                            .ForEachAsync(c => c.Level = Objects.Player.Permission.User);
                });

                await context.SaveChangesAsync();
            }
        }
    }
}
