using System;
using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore.RCon;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     defines the capabilities of an RCon connection
    /// </summary>
    public interface IRConConnection : IDisposable
    {
        /// <summary>
        ///     sends a query with the instance of the rcon connection
        /// </summary>
        /// <param name="type">type of RCon query to perform</param>
        /// <param name="parameters">optional parameter list</param>
        /// <param name="token"></param>
        /// <param name="onPacketSent">
        ///     optional callback invoked synchronously the moment the request packet
        ///     has been written to the wire (post semaphore + flood-protect, before any
        ///     wait-for-response). Receives the UTC timestamp of the send. Used by the
        ///     latency probe to anchor T1 to actual transmission rather than queue time.
        ///     Invoked once per successful socket send; on retries, fires once per attempt.
        /// </param>
        /// <returns></returns>
        Task<string[]> SendQueryAsync(StaticHelpers.QueryType type, string parameters = "", CancellationToken token = default, Action<DateTime> onPacketSent = null);

        /// <summary>
        ///     sets the rcon parser
        /// </summary>
        /// <param name="config">parser</param>
        void SetConfiguration(IRConParser config);

        /// <summary>
        ///     gets the round-trip time of the last successful query
        /// </summary>
        TimeSpan? LastRtt { get; }
    }
}
