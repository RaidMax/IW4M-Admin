using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Data.Abstractions
{
    public interface ILookupCache<T> where T : class
    {
        Task InitializeAsync();
        Task<T> AddAsync(T item);
        Task<T> FirstAsync(Func<T, bool> query);
        IEnumerable<T> GetAll();
    }
}
