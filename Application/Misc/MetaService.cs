using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Data.Models;

namespace IW4MAdmin.Application.Misc
{
    /// <summary>
    /// implementation of IMetaService
    /// used to add and retrieve runtime and persistent meta
    /// </summary>
    [Obsolete("Use MetaServiceV2")]
    public class MetaService : IMetaService
    {
        private readonly IDictionary<MetaType, List<dynamic>> _metaActions;
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly ILogger _logger;

        public MetaService(ILogger<MetaService> logger, IDatabaseContextFactory contextFactory)
        {
            _logger = logger;
            _metaActions = new Dictionary<MetaType, List<dynamic>>();
            _contextFactory = contextFactory;
        }

        public async Task AddPersistentMeta(string metaKey, string metaValue, EFClient client, EFMeta linkedMeta = null)
        {
            // this seems to happen if the client disconnects before they've had time to authenticate and be added
            if (client.ClientId < 1)
            {
                return;
            }

            await using var ctx = _contextFactory.CreateContext();

            var existingMeta = await ctx.EFMeta
                .Where(_meta => _meta.Key == metaKey)
                .Where(_meta => _meta.ClientId == client.ClientId)
                .FirstOrDefaultAsync();

            if (existingMeta != null)
            {
                existingMeta.Value = metaValue;
                existingMeta.Updated = DateTime.UtcNow;
                existingMeta.LinkedMetaId = linkedMeta?.MetaId;
            }

            else
            {
                ctx.EFMeta.Add(new EFMeta()
                {
                    ClientId = client.ClientId,
                    Created = DateTime.UtcNow,
                    Key = metaKey,
                    Value = metaValue,
                    LinkedMetaId = linkedMeta?.MetaId
                });
            }

            await ctx.SaveChangesAsync();
        }

        public async Task SetPersistentMeta(string metaKey, string metaValue, int clientId)
        {
            await AddPersistentMeta(metaKey, metaValue, new EFClient { ClientId = clientId });
        }

        public async Task IncrementPersistentMeta(string metaKey, int incrementAmount, int clientId)
        {
            var existingMeta = await GetPersistentMeta(metaKey, new EFClient { ClientId = clientId });
            
            if (!long.TryParse(existingMeta?.Value ?? "0", out var existingValue))
            {
                existingValue = 0;
            }

            var newValue = existingValue + incrementAmount;
            await SetPersistentMeta(metaKey, newValue.ToString(), clientId);
        }

        public async Task DecrementPersistentMeta(string metaKey, int decrementAmount, int clientId)
        {
            await IncrementPersistentMeta(metaKey, -decrementAmount, clientId);
        }

        public async Task AddPersistentMeta(string metaKey, string metaValue)
        {
            await using var ctx = _contextFactory.CreateContext();

            var existingMeta = await ctx.EFMeta
                .Where(meta => meta.Key == metaKey)
                .Where(meta => meta.ClientId == null)
                .ToListAsync();

            var matchValues = existingMeta
                .Where(meta => meta.Value == metaValue)
                .ToArray();

            if (matchValues.Any())
            {
                foreach (var meta in matchValues)
                {
                    _logger.LogDebug("Updating existing meta with key {key} and id {id}", meta.Key, meta.MetaId);
                    meta.Value = metaValue;
                    meta.Updated = DateTime.UtcNow;
                }

                await ctx.SaveChangesAsync();
            }

            else
            {
                _logger.LogDebug("Adding new meta with key {key}", metaKey);

                ctx.EFMeta.Add(new EFMeta()
                {
                    Created = DateTime.UtcNow,
                    Key = metaKey,
                    Value = metaValue
                });

                await ctx.SaveChangesAsync();
            }
        }

        public async Task RemovePersistentMeta(string metaKey, EFClient client)
        {
            await using var context = _contextFactory.CreateContext();

            var existingMeta = await context.EFMeta
                .FirstOrDefaultAsync(meta => meta.Key == metaKey && meta.ClientId == client.ClientId);

            if (existingMeta == null)
            {
                _logger.LogDebug("No meta with key {key} found for client id {id}", metaKey, client.ClientId);
                return;
            }

            _logger.LogDebug("Removing meta for key {key} with id {id}", metaKey, existingMeta.MetaId);
            context.EFMeta.Remove(existingMeta);
            await context.SaveChangesAsync();
        }

        public async Task RemovePersistentMeta(string metaKey, string metaValue = null)
        {
            await using var context = _contextFactory.CreateContext(enableTracking: false);
            var existingMeta = await context.EFMeta
                .Where(meta => meta.Key == metaKey)
                .Where(meta => meta.ClientId == null)
                .ToListAsync();

            if (metaValue == null)
            {
                _logger.LogDebug("Removing all meta for key {key} with ids [{ids}] ", metaKey, string.Join(", ", existingMeta.Select(meta => meta.MetaId)));
                existingMeta.ForEach(meta => context.Remove(existingMeta));
                await context.SaveChangesAsync();
                return;
            }

            var foundMeta = existingMeta.FirstOrDefault(meta => meta.Value == metaValue);

            if (foundMeta != null)
            {
                _logger.LogDebug("Removing meta for key {key} with id {id}", metaKey, foundMeta.MetaId);
                context.Remove(foundMeta);
                await context.SaveChangesAsync();
            }
        }

        public async Task<EFMeta> GetPersistentMeta(string metaKey, EFClient client)
        {
            await using var ctx = _contextFactory.CreateContext(enableTracking: false);

            return await ctx.EFMeta
                .Where(_meta => _meta.Key == metaKey)
                .Where(_meta => _meta.ClientId == client.ClientId)
                .Select(_meta => new EFMeta()
                {
                    MetaId = _meta.MetaId,
                    Key = _meta.Key,
                    ClientId = _meta.ClientId,
                    Value = _meta.Value,
                    LinkedMetaId = _meta.LinkedMetaId,
                    LinkedMeta = _meta.LinkedMetaId != null ? new EFMeta()
                    {
                        MetaId = _meta.LinkedMeta.MetaId,
                        Key = _meta.LinkedMeta.Key,
                        Value = _meta.LinkedMeta.Value
                    } : null
                })
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<EFMeta>> GetPersistentMeta(string metaKey)
        {
            await using var context = _contextFactory.CreateContext(enableTracking: false);
            return await context.EFMeta
                .Where(meta => meta.Key == metaKey)
                .Where(meta => meta.ClientId == null)
                .Select(meta => new EFMeta
                {
                    MetaId = meta.MetaId,
                    Key = meta.Key,
                    ClientId = meta.ClientId,
                    Value = meta.Value,
                })
                .ToListAsync();
        }

        public void AddRuntimeMeta<T, V>(MetaType metaKey, Func<T, Task<IEnumerable<V>>> metaAction) where T : PaginationRequest where V : IClientMeta
        {
            if (!_metaActions.ContainsKey(metaKey))
            {
                _metaActions.Add(metaKey, new List<dynamic>() { metaAction });
            }

            else
            {
                _metaActions[metaKey].Add(metaAction);
            }
        }

        public async Task<IEnumerable<IClientMeta>> GetRuntimeMeta(ClientPaginationRequest request)
        {
            var metas = await Task.WhenAll(_metaActions.Where(kvp => kvp.Key != MetaType.Information)
                .Select(async kvp => await kvp.Value[0](request)));

            return metas.SelectMany(m => (IEnumerable<IClientMeta>)m)
                .OrderByDescending(m => m.When)
                .Take(request.Count)
                .ToList();
        }

        public async Task<IEnumerable<T>> GetRuntimeMeta<T>(ClientPaginationRequest request, MetaType metaType) where T : IClientMeta
        {
            if (metaType == MetaType.Information)
            {
                var allMeta = new List<T>();

                var completedMeta = await Task.WhenAll(_metaActions[metaType].Select(async individualMetaRegistration =>
                    (IEnumerable<T>)await individualMetaRegistration(request)));
                
                allMeta.AddRange(completedMeta.SelectMany(meta => meta));
                
                return ProcessInformationMeta(allMeta);
            }

            var meta = await _metaActions[metaType][0](request) as IEnumerable<T>;

            return meta;
        }

        private static IEnumerable<T> ProcessInformationMeta<T>(IEnumerable<T> meta) where T : IClientMeta
        {
            var table = new List<List<T>>();
            var metaWithColumn = meta
                .Where(_meta => _meta.Column != null);

            var columnGrouping = metaWithColumn
                .GroupBy(_meta => _meta.Column);

            var metaToSort = meta.Except(metaWithColumn).ToList();

            foreach (var metaItem in columnGrouping)
            {
                table.Add(new List<T>(metaItem));
            }

            while (metaToSort.Count > 0)
            {
                var sortingMeta = metaToSort.First();

                int indexOfSmallestColumn()
                {
                    int index = 0;
                    int smallestColumnSize = int.MaxValue;
                    for (int i = 0; i < table.Count; i++)
                    {
                        if (table[i].Count < smallestColumnSize)
                        {
                            smallestColumnSize = table[i].Count;
                            index = i;
                        }
                    }
                    return index;
                }

                int columnIndex = indexOfSmallestColumn();

                sortingMeta.Column = columnIndex;
                sortingMeta.Order = columnGrouping
                    .First(_group => _group.Key == columnIndex)
                    .Count();

                table[columnIndex].Add(sortingMeta);

                metaToSort.Remove(sortingMeta);
            }

            return meta;
        }
    }
}
