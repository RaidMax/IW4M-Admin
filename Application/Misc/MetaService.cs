using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IW4MAdmin.Application.Misc
{
    /// <summary>
    /// implementation of IMetaService
    /// used to add and retrieve runtime and persistent meta
    /// </summary>
    public class MetaService : IMetaService
    {
        private readonly IDictionary<MetaType, List<dynamic>> _metaActions;
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly ILogger _logger;

        public MetaService(ILogger logger, IDatabaseContextFactory contextFactory)
        {
            _logger = logger;
            _metaActions = new Dictionary<MetaType, List<dynamic>>();
            _contextFactory = contextFactory;
        }

        public async Task AddPersistentMeta(string metaKey, string metaValue, EFClient client)
        {
            // this seems to happen if the client disconnects before they've had time to authenticate and be added
            if (client.ClientId < 1)
            {
                return;
            }

            using var ctx = _contextFactory.CreateContext();

            var existingMeta = await ctx.EFMeta
                .Where(_meta => _meta.Key == metaKey)
                .Where(_meta => _meta.ClientId == client.ClientId)
                .FirstOrDefaultAsync();

            if (existingMeta != null)
            {
                existingMeta.Value = metaValue;
                existingMeta.Updated = DateTime.UtcNow;
            }

            else
            {
                ctx.EFMeta.Add(new EFMeta()
                {
                    ClientId = client.ClientId,
                    Created = DateTime.UtcNow,
                    Key = metaKey,
                    Value = metaValue
                });
            }

            await ctx.SaveChangesAsync();
        }

        public async Task<EFMeta> GetPersistentMeta(string metaKey, EFClient client)
        {
            using var ctx = _contextFactory.CreateContext(enableTracking: false);

            return await ctx.EFMeta
                .Where(_meta => _meta.Key == metaKey)
                .Where(_meta => _meta.ClientId == client.ClientId)
                .Select(_meta => new EFMeta()
                {
                    MetaId = _meta.MetaId,
                    Key = _meta.Key,
                    ClientId = _meta.ClientId,
                    Value = _meta.Value
                })
                .FirstOrDefaultAsync();
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
            var meta = new List<IClientMeta>();

            foreach (var (type, actions) in _metaActions)
            {
                // information is not listed chronologically
                if (type != MetaType.Information)
                {
                    var metaItems = await actions[0](request);
                    meta.AddRange(metaItems);
                }
            }

            return meta.OrderByDescending(_meta => _meta.When)
                .Take(request.Count)
                .ToList();
        }

        public async Task<IEnumerable<T>> GetRuntimeMeta<T>(ClientPaginationRequest request, MetaType metaType) where T : IClientMeta
        {
            IEnumerable<T> meta;
            if (metaType == MetaType.Information)
            {
                var allMeta = new List<T>();

                foreach (var individualMetaRegistration in _metaActions[metaType])
                {
                    allMeta.AddRange(await individualMetaRegistration(request));
                }

                return ProcessInformationMeta(allMeta);
            }

            else
            {
                meta = await _metaActions[metaType][0](request) as IEnumerable<T>;
            }

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
