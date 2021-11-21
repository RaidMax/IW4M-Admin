using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace Data.Helpers
{
    public class DataValueCache<TEntityType, TReturnType> : IDataValueCache<TEntityType, TReturnType>
        where TEntityType : class
    {
        private readonly ILogger _logger;
        private readonly IDatabaseContextFactory _contextFactory;

        private readonly ConcurrentDictionary<string, CacheState<TReturnType>> _cacheStates =
            new ConcurrentDictionary<string, CacheState<TReturnType>>();

        private bool _autoRefresh;
        private const int DefaultExpireMinutes = 15;
        private Timer _timer;

        private class CacheState<TCacheType>
        {
            public string Key { get; set; }
            public DateTime LastRetrieval { get; set; }
            public TimeSpan ExpirationTime { get; set; }
            public Func<DbSet<TEntityType>, CancellationToken, Task<TCacheType>> Getter { get; set; }
            public TCacheType Value { get; set; }
            public bool IsSet { get; set; }

            public bool IsExpired => ExpirationTime != TimeSpan.MaxValue &&
                                     (DateTime.Now - LastRetrieval.Add(ExpirationTime)).TotalSeconds > 0;
        }

        public DataValueCache(ILogger<DataValueCache<TEntityType, TReturnType>> logger,
            IDatabaseContextFactory contextFactory)
        {
            _logger = logger;
            _contextFactory = contextFactory;
        }

        ~DataValueCache()
        {
            _timer?.Stop();
            _timer?.Dispose();
        }

        public void SetCacheItem(Func<DbSet<TEntityType>, CancellationToken, Task<TReturnType>> getter, string key,
            TimeSpan? expirationTime = null, bool autoRefresh = false)
        {
            if (_cacheStates.ContainsKey(key))
            {
                _logger.LogDebug("Cache key {Key} is already added", key);
                return;
            }

            var state = new CacheState<TReturnType>
            {
                Key = key,
                Getter = getter,
                ExpirationTime = expirationTime ?? TimeSpan.FromMinutes(DefaultExpireMinutes)
            };

            _autoRefresh = autoRefresh;

            _cacheStates.TryAdd(key, state);

            if (!_autoRefresh || expirationTime == TimeSpan.MaxValue)
            {
                return;
            }

            _timer = new Timer(state.ExpirationTime.TotalMilliseconds);
            _timer.Elapsed += async (sender, args) => await RunCacheUpdate(state, CancellationToken.None);
            _timer.Start();
        }

        public async Task<TReturnType> GetCacheItem(string keyName, CancellationToken cancellationToken = default)
        {
            if (!_cacheStates.ContainsKey(keyName))
            {
                throw new ArgumentException("No cache found for key {key}", keyName);
            }

            var state = _cacheStates[keyName];

            // when auto refresh is off we want to check the expiration and value
            // when auto refresh is on, we want to only check the value, because it'll be refreshed automatically
            if ((state.IsExpired || !state.IsSet) && !_autoRefresh || _autoRefresh && !state.IsSet)
            {
                await RunCacheUpdate(state, cancellationToken);
            }

            return state.Value;
        }

        private async Task RunCacheUpdate(CacheState<TReturnType> state, CancellationToken token)
        {
            try
            {
                _logger.LogDebug("Running update for {ClassName} {@State}", GetType().Name, state);
                await using var context = _contextFactory.CreateContext(false);
                var set = context.Set<TEntityType>();
                var value = await state.Getter(set, token);
                state.Value = value;
                state.IsSet = true;
                state.LastRetrieval = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not get cached value for {Key}", state.Key);
            }
        }
    }
}