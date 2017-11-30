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
            throw new Exception();
            using (var context = new DatabaseContext())
            {
                var alias = new EFAlias()
                {
                    Active = true,
                    DateAdded = DateTime.UtcNow,
                    IPAddress = entity.IPAddress,
                    Name = entity.Name
                };

                entity.Link = await context.AliasLinks
                    .FirstAsync(a => a.AliasLinkId == entity.Link.AliasLinkId);
                context.Aliases.Add(entity);
                await context.SaveChangesAsync();
                return entity;
            }
        }

        public Task<EFAlias> CreateProxy()
        {
            return null;
        }

        public async Task<EFAlias> Delete(EFAlias entity)
        {
            using (var context = new DatabaseContext())
            {
                var alias = context.Aliases
                    .Single(e => e.AliasId == entity.AliasId);
                alias.Active = false;
                await context.SaveChangesAsync();
                return entity;
            }
        }

        public async Task<IList<EFAlias>> Find(Func<EFAlias, bool> expression)
        {
            using (var context = new DatabaseContext())
                return await Task.Run(() => context.Aliases
                .AsNoTracking()
                .Include(a => a.Link.Children)
                .Where(expression).ToList());
        }

        public async Task<EFAlias> Get(int entityID)
        {
            using (var context = new DatabaseContext())
                return await context.Aliases
                    .AsNoTracking()
                    .SingleOrDefaultAsync(e => e.AliasId == entityID);
        }

        public Task<EFAlias> GetUnique(string entityProperty)
        {
            throw new NotImplementedException();
        }

        public async Task<EFAlias> Update(EFAlias entity)
        {
            throw new Exception();
            using (var context = new DatabaseContext())
            {
                entity = context.Aliases.Attach(entity);
                context.Entry(entity).State = EntityState.Modified;
                await context.SaveChangesAsync();
                return entity;
            }
        }

        public async Task<EFAliasLink> CreateLink(EFAliasLink link)
        {
            using (var context = new DatabaseContext())
            {
                context.AliasLinks.Add(link);
                await context.SaveChangesAsync();
                return link;
            }
        }
    }
}
