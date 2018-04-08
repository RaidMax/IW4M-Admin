using SharedLibraryCore.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharedLibraryCore.Services
{
    public class MetaService
    {
        private static List<Func<int, Task<List<ProfileMeta>>>> MetaActions = new List<Func<int, Task<List<ProfileMeta>>>>();

        public static void AddMeta(Func<int, Task<List<ProfileMeta>>> metaAction)
        {
            MetaActions.Add(metaAction);
        }

        public static async Task<List<ProfileMeta>> GetMeta(int clientId)
        {
            var meta = new List<ProfileMeta>();
            foreach (var action in MetaActions)
                meta.AddRange(await action(clientId));
            return meta;
        }
    }
}
