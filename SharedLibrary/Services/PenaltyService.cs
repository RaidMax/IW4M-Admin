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
            using (var context = new IW4MAdminDatabaseContext())
            {
                entity.Offender = context.Clients.First(e => e.ClientId == entity.Offender.ClientId);
                entity.Punisher = context.Clients.First(e => e.ClientId == entity.Punisher.ClientId);
                entity.Link = context.AliasLinks.First(l => l.AliasLinkId == entity.Link.AliasLinkId);
                if (entity.Expires == DateTime.MinValue)
                    entity.Expires = DateTime.Parse(System.Data.SqlTypes.SqlDateTime.MaxValue.ToString());
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
            using (var context = new IW4MAdminDatabaseContext())
            {
                return await Task.Run(() => context.Penalties
                .Include(p => p.Offender)
                .Include(p => p.Punisher)
                .Where(expression)
                .Where(p => p.Active)
                .ToList());
            }
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
            using (var context = new IW4MAdminDatabaseContext())
                return await context.Penalties
                   .Include(p => p.Offender)
                   .Include(p => p.Punisher)
                  .Where(p => p.Active)
                   .OrderByDescending(p => p.When)
                   .Skip(offset)
                   .Take(count)
                  .ToListAsync();
        }

        public async Task<IList<EFPenalty>> GetClientPenaltiesAsync(int clientId)
        {
            using (var context = new IW4MAdminDatabaseContext())
                return await context.Penalties
                .Where(p => p.OffenderId == clientId)
               .Where(p => p.Active)
               .Include(p => p.Offender)
              .Include(p => p.Punisher)
              .ToListAsync();
        }

        public async Task RemoveActivePenalties(int aliasLinkId)
        {
            using (var context = new IW4MAdminDatabaseContext())
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
                    var clients = await context.Clients.Where(cl => cl.AliasLinkId == p.LinkId).ToListAsync();
                    foreach (var c in clients)
                        if (c.Level == Objects.Player.Permission.Banned)
                            c.Level = Objects.Player.Permission.User;
                });

                await context.SaveChangesAsync();
            }
        }
    }
}
