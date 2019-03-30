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
                return meta;
            }

            return meta.OrderByDescending(_meta => _meta.When)
                .Take(count)
                .ToList();
        }
    }
}
