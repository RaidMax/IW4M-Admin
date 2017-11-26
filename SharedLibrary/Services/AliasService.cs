using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Data.Entity;

using SharedLibrary.Interfaces;
using SharedLibrary.Database.Models;
using SharedLibrary.Database;

namespace SharedLibrary.Services
{
    public class AliasService : IEntityService<EFAlias>
    {
        public async Task<EFAlias> Create(EFAlias entity)
        {
            using (var context = new IW4MAdminDatabaseContext())
            {
                entity.Link = await context.AliasLinks.FirstAsync(a => a.AliasLinkId == entity.Link.AliasLinkId);
                var addedEntity = context.Aliases.Add(entity);
                await context.SaveChangesAsync();
                return addedEntity;
            }
        }

        public Task<EFAlias> CreateProxy()
        {
            return null;
        }

        public async Task<EFAlias> Delete(EFAlias entity)
        {
            using (var context = new IW4MAdminDatabaseContext())
            {
                entity = context.Aliases.Single(e => e.AliasId == entity.AliasId);
                entity.Active = false;
                context.Entry(entity).State = EntityState.Modified;
                await context.SaveChangesAsync();
                return entity;
            }
        }

        public async Task<IList<EFAlias>> Find(Func<EFAlias, bool> expression)
        {
            using (var context = new IW4MAdminDatabaseContext())
                return await Task.Run(() => context.Aliases.Where(expression).ToList());
        }

        public async Task<EFAlias> Get(int entityID)
        {
            using (var context = new IW4MAdminDatabaseContext())
                return await context.Aliases
                    .SingleOrDefaultAsync(e => e.AliasId == entityID);
        }

        public Task<EFAlias> GetUnique(string entityProperty)
        {
            throw new NotImplementedException();
        }

        public async Task<EFAlias> Update(EFAlias entity)
        {
            using (var context = new IW4MAdminDatabaseContext())
            {
                entity = context.Aliases.Attach(entity);
                context.Entry(entity).State = EntityState.Modified;
                await context.SaveChangesAsync();
                return entity;
            }
        }

        public async Task<EFAliasLink> CreateLink(EFAliasLink link)
        {
            using (var context = new IW4MAdminDatabaseContext())
            {
                context.AliasLinks.Add(link);
                await context.SaveChangesAsync();
                return link;
            }
        }
    }
}
