using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharedLibrary.Database;

namespace SharedLibrary.Services
{
   public class GenericService<T> : Interfaces.IEntityService<T>
    {
        public async Task<T> Create(T entity)
        {
            using (var context = new DatabaseContext())
            {
                var dbSet = context.Set(entity.GetType());
                T addedEntity = (T)dbSet.Add(entity);
                await context.SaveChangesAsync();

                return addedEntity;
           }
        }

        public Task<T> CreateProxy()
        {
            throw new NotImplementedException();
        }

        public Task<T> Delete(T entity)
        {
            throw new NotImplementedException();
        }

        public Task<IList<T>> Find(Func<T, bool> expression)
        {
            throw new NotImplementedException();
        }

        public async Task<T> Get(int entityID)
        {
            using (var context = new DatabaseContext())
            {
                var dbSet = context.Set(typeof(T));
                return (T)(await dbSet.FindAsync(entityID));
            }
        }

        public async Task<T> Get(params object[] entityKeys)
        {
            using (var context = new DatabaseContext())
            {
                var dbSet = context.Set(typeof(T));
                return (T)(await dbSet.FindAsync(entityKeys));
            }
        }

        public Task<T> GetUnique(string entityProperty)
        {
            throw new NotImplementedException();
        }

        public Task<T> Update(T entity)
        {
            throw new NotImplementedException();
        }
    }
}
