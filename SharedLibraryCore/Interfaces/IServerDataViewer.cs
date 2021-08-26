using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// Exposes methods to get analytical data about server(s)
    /// </summary>
    public interface IServerDataViewer
    {
        /// <summary>
        /// Retrieves the max concurrent clients over a give time period for all servers or given server id
        /// </summary>
        /// <param name="serverId">ServerId to query on</param>
        /// <param name="overPeriod">how far in the past to search</param>
        /// <param name="token">CancellationToken</param>
        /// <returns></returns>
        Task<int> MaxConcurrentClientsAsync(long? serverId = null, TimeSpan? overPeriod = null, CancellationToken token = default);
        
        /// <summary>
        /// Gets the total number of clients connected and total clients connected in the given time frame
        /// </summary>
        /// <param name="overPeriod">how far in the past to search</param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<(int, int)> ClientCountsAsync(TimeSpan? overPeriod = null, CancellationToken token = default);
    }
}