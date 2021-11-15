using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Data.Abstractions
{
    public interface IDataValueCache<TEntityType, TReturnType> where TEntityType : class
    {
        void SetCacheItem(Func<DbSet<TEntityType>, CancellationToken, Task<TReturnType>> itemGetter, string keyName,
            TimeSpan? expirationTime = null, bool autoRefresh = false);
        Task<TReturnType> GetCacheItem(string keyName, CancellationToken token = default);
    }
}