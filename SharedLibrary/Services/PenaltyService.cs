using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity;

using SharedLibrary.Database;
using SharedLibrary.Database.Models;
using System.Linq.Expressions;

namespace SharedLibrary.Services
{
    public class PenaltyService : Interfaces.IEntityService<EFPenalty>
    {
        public async Task<EFPenalty> Create(EFPenalty entity)
        {
            using (var context = new DatabaseContext())
            {
                entity.Offender = context.Clients.First(e => e.ClientId == entity.Offender.ClientId);
                entity.Punisher = context.Clients.First(e => e.ClientId == entity.Punisher.ClientId);
                entity.Link = context.AliasLinks.First(l => l.AliasLinkId == entity.Link.AliasLinkId);

                // make bans propogate to all aliases
                if (entity.Type == Objects.Penalty.PenaltyType.Ban)
                {
                    entity.Expires = DateTime.Parse(System.Data.SqlTypes.SqlDateTime.MaxValue.ToString());
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
            return await Task.Run(() =>
            {
                using (var context = new DatabaseContext())
                    return context.Penalties
                    .Include(p => p.Offender)
                    .Include(p => p.Punisher)
                    .Where(expression)
                    .Where(p => p.Active)
                    .ToList();
            });
        }

        public Task<EFPenalty> Get(int entityID)
        {
            throw new NotImplementedException();
        }

        public Task<EFPenalty> GetUnique(string entityProperty)
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
