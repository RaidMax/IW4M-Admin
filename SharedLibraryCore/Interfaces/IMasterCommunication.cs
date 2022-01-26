using System.Threading;
using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     defines the capabilities of the communication to the master server
    /// </summary>
    public interface IMasterCommunication
    {
        /// <summary>
        ///     checks the current version of IW4MAdmin against the master version
        /// </summary>
        /// <returns></returns>
        Task CheckVersion();

        /// <summary>
        ///     Sends heart beats to the master
        /// </summary>
        /// <param name="token">Cancellation token</param>
        /// <returns></returns>
        Task RunUploadStatus(CancellationToken token);
    }
}