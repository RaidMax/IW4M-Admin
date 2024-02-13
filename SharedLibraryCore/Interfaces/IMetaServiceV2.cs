using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.QueryHelper;

namespace SharedLibraryCore.Interfaces;

public interface IMetaServiceV2
{
    #region PER_CLIENT

    /// <summary>
    /// adds or updates meta key and value to the database as simple string
    /// </summary>
    /// <param name="metaKey">key of meta data</param>
    /// <param name="metaValue">value of the meta data</param>
    /// <param name="clientId">id of the client to save the meta for</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task SetPersistentMeta(string metaKey, string metaValue, int clientId, CancellationToken token = default);

    /// <summary>
    /// add or update meta key and value to the database as serialized object type
    /// </summary>
    /// <param name="metaKey"></param>
    /// <param name="metaValue"></param>
    /// <param name="clientId"></param>
    /// <param name="token"></param>
    /// <typeparam name="T">type of object being serialized</typeparam>
    /// <returns></returns>
    Task SetPersistentMetaValue<T>(string metaKey, T metaValue, int clientId, CancellationToken token = default)
        where T : class;

    /// <summary>
    /// Sets meta key to a linked lookup key and id
    /// </summary>
    /// <param name="metaKey">Key for the client meta</param>
    /// <param name="lookupKey">Key of the global lookup meta</param>
    /// <param name="lookupId">Id in the list of lookup values</param>
    /// <param name="clientId">id of the client</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task SetPersistentMetaForLookupKey(string metaKey, string lookupKey, int lookupId, int clientId,
        CancellationToken token = default);

    /// <summary>
    /// increments meta value and persists to the database
    /// <remarks>if the meta value does not already exist it will be set to the increment amount</remarks>
    /// <remarks>the assumption is made that the existing value is <see cref="int"/></remarks>
    /// </summary>
    /// <param name="metaKey">key of meta data</param>
    /// <param name="incrementAmount">value to increment by</param>
    /// <param name="clientId">id of the client to save the meta for</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task IncrementPersistentMeta(string metaKey, int incrementAmount, int clientId,
        CancellationToken token = default);

    /// <summary>
    /// decrements meta value and persists to the database
    /// <remarks>if the meta value does not already exist it will be set to the decrement amount</remarks>
    /// <remarks>the assumption is made that the existing value is <see cref="int"/></remarks>
    /// </summary>
    /// <param name="metaKey">key of meta data</param>
    /// <param name="decrementAmount">value to increment by</param>
    /// <param name="clientId">id of the client to save the meta for</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task DecrementPersistentMeta(string metaKey, int decrementAmount, int clientId,
        CancellationToken token = default);

    /// <summary>
    /// retrieves meta entry
    /// </summary>
    /// <param name="metaKey"></param>
    /// <param name="clientId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<EFMeta> GetPersistentMeta(string metaKey, int clientId, CancellationToken token = default);

    /// <summary>
    /// retrieves meta value as deserialized object
    /// </summary>
    /// <param name="metaKey"></param>
    /// <param name="clientId"></param>
    /// <param name="token"></param>
    /// <typeparam name="T">object type to deserialize into</typeparam>
    /// <returns></returns>
    Task<T?> GetPersistentMetaValue<T>(string metaKey, int clientId, CancellationToken token = default)
        where T : class;

    /// <summary>
    /// retrieves meta entry by with associated lookup value as string
    /// </summary>
    /// <param name="metaKey"></param>
    /// <param name="lookupKey"></param>
    /// <param name="clientId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<EFMeta> GetPersistentMetaByLookup(string metaKey, string lookupKey, int clientId,
        CancellationToken token = default);

    /// <summary>
    /// removes meta key with given value
    /// </summary>
    /// <param name="metaKey">key of meta data</param>
    /// <param name="clientId">client to delete the meta for</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task RemovePersistentMeta(string metaKey, int clientId, CancellationToken token = default);

    #endregion

    #region GLOBAL

    /// <summary>
    /// adds or updates meta key and value to the database
    /// </summary>
    /// <param name="metaKey">key of meta data</param>
    /// <param name="metaValue">value of the meta data</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task SetPersistentMeta(string metaKey, string metaValue, CancellationToken token = default);

    /// <summary>
    /// serializes and sets (create or update) meta key and value
    /// </summary>
    /// <param name="metaKey"></param>
    /// <param name="metaValue"></param>
    /// <param name="token"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    Task SetPersistentMetaValue<T>(string metaKey, T metaValue, CancellationToken token = default) where T : class;

    /// <summary>
    /// removes meta key with given value
    /// </summary>
    /// <param name="metaKey">key of the meta data</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task RemovePersistentMeta(string metaKey, CancellationToken token = default);

    /// <summary>
    /// retrieves collection of meta for given key
    /// </summary>
    /// <param name="metaKey">key to retrieve values for</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<EFMeta> GetPersistentMeta(string metaKey, CancellationToken token = default);

    /// <summary>
    /// returns value of meta key if it exists
    /// </summary>
    /// <param name="metaKey"></param>
    /// <param name="token"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    Task<T> GetPersistentMetaValue<T>(string metaKey, CancellationToken token = default) where T : class;

    #endregion

    /// <summary>
    /// adds a meta task to the runtime meta list
    /// </summary>
    /// <param name="metaKey">type of meta</param>
    /// <param name="metaAction">action to perform</param>
    void AddRuntimeMeta<T, TReturn>(MetaType metaKey,
        Func<T, CancellationToken, Task<IEnumerable<TReturn>>> metaAction)
        where TReturn : IClientMeta where T : PaginationRequest;

    /// <summary>
    /// retrieves all the runtime meta information for given client idea
    /// </summary>
    /// <param name="request">request information</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IEnumerable<IClientMeta>> GetRuntimeMeta(ClientPaginationRequest request, CancellationToken token = default);

    /// <summary>
    /// retrieves all the runtime of provided type
    /// </summary>
    /// <param name="request">>request information</param>
    /// <param name="metaType">type of meta to retreive</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IEnumerable<T>> GetRuntimeMeta<T>(ClientPaginationRequest request, MetaType metaType, CancellationToken token = default)
        where T : IClientMeta;
}
