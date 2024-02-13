using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore.Interfaces
{
    public interface IEntityService<T>
    {
        Task<T> Create(T entity);
        Task<T> Delete(T entity);
        Task<EFClient?> Update(T entity);
        Task<EFClient?> Get(int entityID);
        Task<T?> GetUnique(long entityProperty, object altKey);
        Task<IList<T>> Find(Func<T, bool> expression);
    }
}
