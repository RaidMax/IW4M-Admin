using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Data.Abstractions
{
    public interface IDataValueCache<T, V> where T : class
    {
        void SetCacheItem(Func<DbSet<T>, CancellationToken, Task<V>> itemGetter, string keyName, TimeSpan? expirationTime = null);
        Task<V> GetCacheItem(string keyName, CancellationToken token = default);
    }
}