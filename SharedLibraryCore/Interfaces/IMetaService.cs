using Data.Models;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.QueryHelper;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    public interface IMetaService
    {
        /// <summary>
        /// adds or updates meta key and value to the database
        /// </summary>
        /// <param name="metaKey">key of meta data</param>
        /// <param name="metaValue">value of the meta data</param>
        /// <param name="client">client to save the meta for</param>
        /// <returns></returns>
        Task AddPersistentMeta(string metaKey, string metaValue, EFClient client, EFMeta linkedMeta = null);

        /// <summary>
        /// adds or updates meta key and value to the database
        /// </summary>
        /// <param name="metaKey">key of meta data</param>
        /// <param name="metaValue">value of the meta data</param>
        /// <returns></returns>
        Task AddPersistentMeta(string metaKey, string metaValue);

        /// <summary>
        ///  removes meta key with given value
        /// </summary>
        /// <param name="metaKey">key of meta data</param>
        /// <param name="client">client to delete the meta for</param>
        /// <returns></returns>
        Task RemovePersistentMeta(string metaKey, EFClient client);

        /// <summary>
        /// removes meta key with given value
        /// </summary>
        /// <param name="metaKey">key of the meta data</param>
        /// <param name="metaValue">value of the meta data</param>
        /// <returns></returns>
        Task RemovePersistentMeta(string metaKey, string metaValue = null);

        /// <summary>
        /// retrieves meta data for given client and key
        /// </summary>
        /// <param name="metaKey">key to retrieve value for</param>
        /// <param name="client">client to retrieve meta for</param>
        /// <returns></returns>
        Task<EFMeta> GetPersistentMeta(string metaKey, EFClient client);

        /// <summary>
        /// retrieves collection of meta for given key
        /// </summary>
        /// <param name="metaKey">key to retrieve values for</param>
        /// <returns></returns>
        Task<IEnumerable<EFMeta>> GetPersistentMeta(string metaKey);

        /// <summary>
        /// adds a meta task to the runtime meta list
        /// </summary>
        /// <param name="metaKey">type of meta</param>
        /// <param name="metaAction">action to perform</param>
        void AddRuntimeMeta<T,V>(MetaType metaKey, Func<T, Task<IEnumerable<V>>> metaAction) where V : IClientMeta where T: PaginationRequest;

        /// <summary>
        /// retrieves all the runtime meta information for given client idea
        /// </summary>
        /// <param name="request">request information</param>
        /// <returns></returns>
        Task<IEnumerable<IClientMeta>> GetRuntimeMeta(ClientPaginationRequest request);

        /// <summary>
        /// retreives all the runtime of provided type 
        /// </summary>
        /// <param name="request">>request information</param>
        /// <param name="metaType">type of meta to retreive</param>
        /// <returns></returns>
        Task<IEnumerable<T>> GetRuntimeMeta<T>(ClientPaginationRequest request, MetaType metaType) where T : IClientMeta;
    }
}
