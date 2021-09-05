using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    public interface IServerDataCollector
    {
        /// <summary>
        /// Begins to collection on servers for analytical purposes
        /// </summary>
        /// <param name="period">interval at which to collect data</param>
        /// <param name="cancellationToken">Token</param>
        /// <returns>Task</returns>
        Task BeginCollectionAsync(TimeSpan? period = null, CancellationToken cancellationToken = default);
    }
}