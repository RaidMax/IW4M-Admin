using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SharedLibraryCore.Services
{
    public class MetaService
    {
        private static List<Func<int, int, int, DateTime?, Task<List<ProfileMeta>>>> _metaActions = new List<Func<int, int, int, DateTime?, Task<List<ProfileMeta>>>>();

        /// <summary>
        /// adds or updates meta key and value to the database
        /// </summary>
        /// <param name="metaKey">key of meta data</param>
        /// <param name="metaValue">value of the meta data</param>
        /// <param name="client">client to save the meta for</param>
        /// <returns></returns>
        public async Task AddPersistentMeta(string metaKey, string metaValue, EFClient client)
        {
            // this seems to happen if the client disconnects before they've had time to authenticate and be added
            if (client.ClientId < 1)
            {
                return;
            }

            using (var ctx = new DatabaseContext())
            {
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
        }

        internal static void Clear()
        {
            _metaActions.Clear();
        }

        /// <summary>
        /// retrieves meta data for given client and key
        /// </summary>
        /// <param name="metaKey">key to retrieve value for</param>
        /// <param name="client">client to retrieve meta for</param>
        /// <returns></returns>
        public async Task<EFMeta> GetPersistentMeta(string metaKey, EFClient client)
        {
            using (var ctx = new DatabaseContext(disableTracking: true))
            {
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
        }

        /// <summary>
        /// aads a meta task to the runtime meta list
        /// </summary>
        /// <param name="metaAction"></param>
        public static void AddRuntimeMeta(Func<int, int, int, DateTime?, Task<List<ProfileMeta>>> metaAction)
        {
            _metaActions.Add(metaAction);
        }

        /// <summary>
        /// retrieves all the runtime meta information for given client idea
        /// </summary>
        /// <param name="clientId">id of the client</param>
        /// <param name="count">number of meta items to retrieve</param>
        /// <param name="offset">offset from the first item</param>
        /// <returns></returns>
        public static async Task<List<ProfileMeta>> GetRuntimeMeta(int clientId, int offset = 0, int count = int.MaxValue, DateTime? startAt = null)
        {
            var meta = new List<ProfileMeta>();

            foreach (var action in _metaActions)
            {
                var metaItems = await action(clientId, offset, count, startAt);
                meta.AddRange(metaItems);
            }

            if (count == 1)
            {
                var table = new List<List<ProfileMeta>>();
                var metaWithColumn = meta
                    .Where(_meta => _meta.Column != null);

                var columnGrouping = metaWithColumn
                    .GroupBy(_meta => _meta.Column);

                var metaToSort = meta.Except(metaWithColumn).ToList();

                foreach (var metaItem in columnGrouping)
                {
                    table.Add(new List<ProfileMeta>(metaItem));
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

            return meta.OrderByDescending(_meta => _meta.When)
                .Take(count)
                .ToList();
        }
    }
}
