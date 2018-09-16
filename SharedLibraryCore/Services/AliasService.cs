using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;

using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Database;
using Microsoft.EntityFrameworkCore;

namespace SharedLibraryCore.Services
{
    public class AliasService : IEntityService<EFAlias>
    {
        public async Task<EFAlias> Create(EFAlias entity)
        {
            throw await Task.FromResult(new Exception());
            /*using (var context = new DatabaseContext())
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
            }*/
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
            // todo: max better?
            return await Task.Run(() =>
           {
               using (var context = new DatabaseContext(true))
                   return context.Aliases
                   .AsNoTracking()
                   .Include(a => a.Link.Children)
                   .Where(expression)
                   .ToList();
           });
        }

        public async Task<EFAlias> Get(int entityID)
        {
            using (var context = new DatabaseContext(true))
                return await context.Aliases
                    .FirstOrDefaultAsync(e => e.AliasId == entityID);
        }

        public Task<EFAlias> GetUnique(long entityProperty)
        {
            throw new NotImplementedException();
        }

        public async Task<EFAlias> Update(EFAlias entity)
        {
            throw await Task.FromResult(new Exception());
        }
    }
}
