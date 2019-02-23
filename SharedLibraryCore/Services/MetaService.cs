using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SharedLibraryCore.Services
{
    public class MetaService
    {
        private static List<Func<int, Task<List<ProfileMeta>>>> _metaActions = new List<Func<int, Task<List<ProfileMeta>>>>();

        public async Task AddPersistentMeta(string metaKey, string metaValue, EFClient client)
        {
            using (var ctx = new DatabaseContext())
            {
                var existingMeta = await ctx.EFMeta.FirstOrDefaultAsync(_meta => _meta.ClientId == client.ClientId && _meta.Key == metaKey);

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

        public async Task<EFMeta> GetPersistentMeta(string metaKey, EFClient client)
        {
            using (var ctx = new DatabaseContext(disableTracking:true))
            {
                return await ctx.EFMeta
                    .Where(_meta => _meta.Key == metaKey)
                    .Where(_meta => _meta.ClientId == client.ClientId)
                    .FirstOrDefaultAsync();
            }
        }

        public static void AddRuntimeMeta(Func<int, Task<List<ProfileMeta>>> metaAction)
        {
            _metaActions.Add(metaAction);
        }

        public static async Task<List<ProfileMeta>> GetRuntimeMeta(int clientId)
        {
            var meta = new List<ProfileMeta>();

            foreach (var action in _metaActions)
            {
                meta.AddRange(await action(clientId));
            }

            return meta;
        }
    }
}
