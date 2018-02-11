using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Interfaces
{
    public interface IEntityService<T>
    {
        Task<T> CreateProxy();
        Task<T> Create(T entity);
        Task<T> Delete(T entity);
        Task<T> Update(T entity);
        Task<T> Get(int entityID);
        Task<T> GetUnique(long entityProperty);
        Task<IList<T>> Find(Func<T, bool> expression);
    }
}
