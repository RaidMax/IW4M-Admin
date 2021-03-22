using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Data.Helpers
{
    public class DataValueCache<T, V> : IDataValueCache<T, V> where T : class
    {
        private readonly ILogger _logger;
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly Dictionary<string, CacheState> _cacheStates = new Dictionary<string, CacheState>();
        private const int DefaultExpireMinutes = 15;

        private class CacheState
        {
            public string Key { get; set; }
            public DateTime LastRetrieval { get; set; }
            public TimeSpan ExpirationTime { get; set; }
            public Func<DbSet<T>, Task<V>> Getter { get; set; }
            public V Value { get; set; }
            public bool IsExpired => (DateTime.Now - LastRetrieval.Add(ExpirationTime)).TotalSeconds > 0;
        }
        
        public DataValueCache(ILogger<DataValueCache<T, V>> logger, IDatabaseContextFactory contextFactory)
        {
            _logger = logger;
            _contextFactory = contextFactory;
        }
        
        public void SetCacheItem(Func<DbSet<T>, Task<V>> getter, string key, TimeSpan? expirationTime = null)
        {
            if (_cacheStates.ContainsKey(key))
            {
                _logger.LogDebug("Cache key {key} is already added", key);
                return;
            }
                 
            var state = new CacheState()
            {
                Key = key,
                Getter = getter,
                ExpirationTime = expirationTime ?? TimeSpan.FromMinutes(DefaultExpireMinutes)
            };
  
            _cacheStates.Add(key, state);
        }
        
        public async Task<V> GetCacheItem(string keyName)
        {
            if (!_cacheStates.ContainsKey(keyName))
            {
                throw new ArgumentException("No cache found for key {key}", keyName);
            }

            var state = _cacheStates[keyName];

            if (state.IsExpired)
            {
                await RunCacheUpdate(state);
            }

            return state.Value;
        }

        private async Task RunCacheUpdate(CacheState state)
        {
            try
            {
                await using var context = _contextFactory.CreateContext(false);
                var set = context.Set<T>();
                var value = await state.Getter(set);
                state.Value = value;
                state.LastRetrieval = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not get cached value for {key}", state.Key);
            }
        }
    }
}