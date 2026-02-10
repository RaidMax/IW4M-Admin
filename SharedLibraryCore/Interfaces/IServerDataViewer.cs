using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using SharedLibraryCore.Dtos;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     Exposes methods to get analytical data about server(s)
    /// </summary>
    public interface IServerDataViewer
    {
        /// <summary>
        ///     Retrieves the max concurrent clients over a give time period for all servers or given server id
        /// </summary>
        /// <param name="serverId">ServerId to query on</param>
        /// <param name="gameCode"><see cref="Reference.Game"/></param>
        /// <param name="overPeriod">how far in the past to search</param>
        /// <param name="token">CancellationToken</param>
        /// <returns></returns>
        Task<(int?, DateTime?)> MaxConcurrentClientsAsync(long? serverId = null, Reference.Game? gameCode = null, TimeSpan? overPeriod = null,
            CancellationToken token = default);

        /// <summary>
        ///     Gets the total number of clients connected and total clients connected in the given time frame
        /// </summary>
        /// <param name="overPeriod">how far in the past to search</param>
        /// <param name="gameCode"><see cref="Reference.Game"/></param>
        /// <param name="token">CancellationToken</param>
        /// <returns></returns>
        Task<(int, int)> ClientCountsAsync(TimeSpan? overPeriod = null, Reference.Game? gameCode = null, CancellationToken token = default);

        /// <summary>
        ///     Retrieves the client count and history over the given period
        /// </summary>
        /// <param name="overPeriod">how far in the past to search</param>
        /// <param name="token">CancellationToken</param>
        /// <returns></returns>
        Task<IEnumerable<ClientHistoryInfo>> ClientHistoryAsync(TimeSpan? overPeriod = null,
            CancellationToken token = default);

        /// <summary>
        ///     Retrieves the number of ranked clients for given server id
        /// </summary>
        /// <param name="serverId">ServerId to query on</param>
        /// <param name="token">CancellationToken</param>
        /// <returns></returns>
        Task<int> RankedClientsCountAsync(long? serverId = null, CancellationToken token = default);

        /// <summary>
        ///     Retrieves aggregated daily playtime for the server activity sparkline (last N days).
        /// </summary>
        /// <param name="serverId">Optional server id to filter by</param>
        /// <param name="gameCode">Optional game to filter by</param>
        /// <param name="days">Number of days (default 30)</param>
        /// <param name="token">CancellationToken</param>
        /// <returns>Daily playtime minutes and total for the period</returns>
        Task<ServerActivitySparklineResult> GetServerActivityAsync(long? serverId = null, Reference.Game? gameCode = null, int days = 30, CancellationToken token = default);
    }
}
